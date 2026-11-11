using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aiursoft.DbTools;
using Aiursoft.Nupkg.Core.Authentication;
using Aiursoft.Nupkg.Core.Indexing;
using Aiursoft.Nupkg.Entities;
using Aiursoft.Nupkg.Services.SupplyChain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.WebUtilities;
using static Aiursoft.WebTools.Extends;

namespace Aiursoft.Nupkg.Tests.IntegrationTests;

[TestClass]
public class S360MirrorTests : TestBase
{
    private string? _dataPath;
    private WebApplication? _upstream;
    private readonly List<ArtifactIdentity> _approved = [];
    private readonly Dictionary<string, byte[]> _archives = [];
    private readonly HashSet<string> _revoked = new(StringComparer.OrdinalIgnoreCase);
    private bool _emptyFirstPage;
    private bool _unavailable;
    private bool _mismatchedResponse;
    private string _source = string.Empty;
    private int _downloads;
    private TaskCompletionSource<bool>? _downloadStarted;
    private TaskCompletionSource<bool>? _downloadRelease;

    [TestInitialize]
    public override async Task CreateServer()
    {
        using var upstreamPort = new TestPortReservation();
        var originPort = upstreamPort.Port;
        _source = $"http://localhost:{originPort}/v3/index.json";
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://0.0.0.0:{originPort}");
        _upstream = builder.Build();
        upstreamPort.ConfigureServer(_upstream);
        _upstream.MapGet("/v3/index.json", () => Results.Json(new
        {
            version = "3.0.0",
            resources = new[] { new Dictionary<string, string>
            { ["@type"] = "PackageBaseAddress/3.0.0", ["@id"] = $"http://localhost:{originPort}/flat/" },
                new Dictionary<string, string> { ["@type"] = "RegistrationsBaseUrl/3.6.0", ["@id"] = $"http://localhost:{originPort}/registration/" },
                new Dictionary<string, string> { ["@type"] = "SearchQueryService/3.4.0", ["@id"] = $"http://localhost:{originPort}/search" } }
        }));
        _upstream.MapGet("/flat/{id}/{version}/{file}", async (string id, HttpContext context) =>
        {
            Interlocked.Increment(ref _downloads);
            _downloadStarted?.TrySetResult(true);
            if (_downloadRelease != null) await _downloadRelease.Task.WaitAsync(context.RequestAborted);
            return _archives.TryGetValue(id, out var bytes) ? Results.Bytes(bytes) : Results.NotFound();
        });
        _upstream.MapGet("/registration/{id}/index.json", (string id) => Results.Json(new
        {
            count = 1, items = new[] { new { count = 2, lower = "1.2.3", upper = "9.9.9",
                items = new[] { "1.2.3", "9.9.9" }.Select(version => new
                {
                    catalogEntry = new { id, version, listed = true, authors = "Fixture", description = "Upstream metadata" }
                }) } }
        }));
        await _upstream.StartAsync();
        using var port = new TestPortReservation();
        Http.BaseAddress = new Uri($"http://localhost:{port.Port}");
        _dataPath = Directory.CreateTempSubdirectory("NupkgMirror-").FullName;
        Server = await AppAsync<Startup>([
            $"Storage:Path={_dataPath}", "S360Mirror:Enabled=true", "S360Mirror:Server=https://s360.fixture/",
            "S360Mirror:CheckKey=mirror-fixture", "S360Mirror:IntervalMinutes=1440", "Mirror:Enabled=true",
            $"Mirror:PackageSource={_source}", "ApiKey=fixture-publisher"
        ], port: port.Port);
        port.ConfigureServer(Server);
        Server.Services.GetRequiredService<IOptionsMonitor<HttpClientFactoryOptions>>().Get("S360Admission")
            .HttpMessageHandlerBuilderActions.Add(b => b.PrimaryHandler = new AdmissionHandler(HandleAdmissionAsync));
        await Server.UpdateDbAsync<NupkgDbContext>();
        await Server.SeedAsync();
        await Server.StartAsync();
    }

    private async Task<HttpResponseMessage> HandleAdmissionAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Assert.AreEqual("mirror-fixture", request.Headers.GetValues("X-S360-Key").Single());
        if (_unavailable) return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        object result;
        if (request.Method == HttpMethod.Get)
        {
            var query = QueryHelpers.ParseQuery(request.RequestUri!.Query);
            var name = query.TryGetValue("name", out var value) ? value.ToString() : null;
            var approved = _approved.Where(a => !_revoked.Contains(a.Name) &&
                (name == null || a.Name.Equals(name, StringComparison.OrdinalIgnoreCase))).ToList();
            result = _emptyFirstPage && !request.RequestUri.Query.Contains("afterId=1&", StringComparison.Ordinal)
                ? new ApprovedArtifactPage { NextAfterId = 1, UpperId = 100 }
                : new ApprovedArtifactPage { Artifacts = approved, UpperId = 100 };
        }
        else
        {
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            var identities = json.RootElement.GetProperty("dependencies")
                .Deserialize<List<ArtifactIdentity>>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
            result = new ArtifactAssessmentResponse
            {
                Decisions = identities.Select(a => new ArtifactDecision
                {
                    Dependency = _mismatchedResponse ? a with { Sha256 = new string('0', 64) } : a,
                    Trusted = _approved.Contains(a) && !_revoked.Contains(a.Name),
                    Reason = _revoked.Contains(a.Name) ? "Explicitly revoked." : "Approved."
                }).ToList()
            };
        }
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(result) };
    }

    private sealed class AdmissionHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }

    [TestCleanup]
    public override async Task CleanServer()
    {
        await base.CleanServer();
        if (_upstream != null) { await _upstream.StopAsync(); await _upstream.DisposeAsync(); }
        if (_dataPath != null) Directory.Delete(_dataPath, true);
    }

    private byte[] AddArchive(string name, bool approve = true, string? archiveName = null, string version = "1.2.3")
    {
        name = name.ToLowerInvariant();
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, true))
        {
            using (var writer = new StreamWriter(zip.CreateEntry("package.nuspec").Open(), Encoding.UTF8))
                writer.Write($"<package><metadata><id>{archiveName ?? name}</id><version>{version}</version><authors>Fixture</authors><description>Mirror fixture</description><readme>readme.md</readme></metadata></package>");
            using var readme = new StreamWriter(zip.CreateEntry("readme.md").Open());
            readme.Write("# Approved mirror fixture");
        }
        var bytes = buffer.ToArray();
        _archives[name.ToLowerInvariant()] = bytes;
        if (approve) _approved.Add(new ArtifactIdentity("nuget", name, version, _source, Convert.ToHexStringLower(SHA256.HashData(bytes))));
        return bytes;
    }

    private async Task<int> SynchronizeAsync()
    {
        using var scope = Server!.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ApprovedPackageMirror>().SynchronizeAsync(CancellationToken.None);
    }

    [TestMethod]
    public async Task ApprovedArchivesAreAutomaticallyMirroredAndRevocationHidesEveryReadPath()
    {
        _emptyFirstPage = true;
        var bytes = AddArchive("Mirror.Approved");
        AddArchive("Mirror.Unapproved", approve: false);
        Assert.AreEqual(1, await SynchronizeAsync());
        Assert.AreEqual(0, await SynchronizeAsync());
        Assert.AreEqual(1, _downloads);
        CollectionAssert.AreEqual(bytes, await Http.GetByteArrayAsync("/v3/package/mirror.approved/1.2.3/mirror.approved.1.2.3.nupkg"));
        Assert.Contains("1.2.3", await Http.GetStringAsync("/v3/package/mirror.approved/index.json"));
        Assert.AreEqual(HttpStatusCode.OK, (await Http.GetAsync("/v3/registration/mirror.approved/index.json")).StatusCode);
        _revoked.Add("Mirror.Approved");
        foreach (var suffix in new[] { "mirror.approved.1.2.3.nupkg", "mirror.approved.nuspec", "readme", "icon" })
        {
            var denied = await Http.GetAsync("/v3/package/mirror.approved/1.2.3/" + suffix);
            Assert.AreEqual(HttpStatusCode.Forbidden, denied.StatusCode, suffix);
            Assert.Contains("revoked", await denied.Content.ReadAsStringAsync());
        }
        Assert.AreEqual(HttpStatusCode.NotFound, (await Http.GetAsync("/v3/package/mirror.approved/index.json")).StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, (await Http.GetAsync("/v3/registration/mirror.approved/index.json")).StatusCode);
        Assert.DoesNotContain("mirror.approved", await Http.GetStringAsync("/v3/search?q=Mirror.Approved"));
        Assert.DoesNotContain("mirror.approved", await Http.GetStringAsync("/packages?q=Mirror.Approved"));
        Assert.AreEqual(HttpStatusCode.Forbidden, (await Http.GetAsync("/v3/package/mirror.unapproved/1.2.3/mirror.unapproved.1.2.3.nupkg")).StatusCode);
        Assert.AreEqual(1, _downloads);
    }

    [TestMethod]
    public async Task WrongHashesAndWrongArchiveIdentityAreNeverIndexed()
    {
        AddArchive("Mirror.Tampered");
        _approved[0] = _approved[0] with { Sha256 = new string('b', 64) };
        AddArchive("Mirror.WrongIdentity", archiveName: "Mirror.SomeoneElse");
        Assert.AreEqual(0, await SynchronizeAsync());
        using var scope = Server!.Services.CreateScope();
        Assert.IsFalse(await scope.ServiceProvider.GetRequiredService<NupkgDbContext>().Packages
            .AnyAsync(p => p.Id == "mirror.tampered" || p.Id == "mirror.wrongidentity" || p.Id == "mirror.someoneelse"));
    }

    [TestMethod]
    public async Task ExistingLegacyCacheNeedsVerifiedAdoptionAndPublishedPackagesStayAvailable()
    {
        var bytes = AddArchive("Mirror.Legacy");
        using (var scope = Server!.Services.CreateScope())
        {
            var indexer = scope.ServiceProvider.GetRequiredService<IPackageIndexingService>();
            await using var stream = new MemoryStream(bytes);
            Assert.AreEqual(PackageIndexingResult.Success, await indexer.IndexAsync(stream, CancellationToken.None));
            var db = scope.ServiceProvider.GetRequiredService<NupkgDbContext>();
            (await db.Packages.SingleAsync(p => p.Id == "mirror.legacy")).IsLocallyPublished = false;
            await db.SaveChangesAsync();
        }
        var artifact = _approved.Single();
        _approved.Clear();
        Assert.AreEqual(HttpStatusCode.Forbidden, (await Http.GetAsync("/v3/package/mirror.legacy/1.2.3/mirror.legacy.1.2.3.nupkg")).StatusCode);
        _approved.Add(artifact);
        Assert.AreEqual(1, await SynchronizeAsync());
        Assert.AreEqual(0, _downloads);
        CollectionAssert.AreEqual(bytes, await Http.GetByteArrayAsync("/v3/package/mirror.legacy/1.2.3/mirror.legacy.1.2.3.nupkg"));
        var published = AddArchive("Company.Published", approve: false);
        using (var request = new HttpRequestMessage(HttpMethod.Put, "/api/v2/package"))
        {
            request.Headers.Add("X-NuGet-ApiKey", "fixture-publisher");
            request.Content = new ByteArrayContent(published);
            Assert.AreEqual(HttpStatusCode.Created, (await Http.SendAsync(request)).StatusCode);
        }
        CollectionAssert.AreEqual(published, await Http.GetByteArrayAsync("/v3/package/company.published/1.2.3/company.published.1.2.3.nupkg"));
    }

    [TestMethod]
    public async Task AutomaticMirroringCannotPublishIntoAPersonallyOwnedName()
    {
        var bytes = AddArchive("Company.Personal", approve: false, version: "1.0.0");
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NupkgDbContext>();
            var owner = await db.Users.FirstAsync();
            await using var stream = new MemoryStream(bytes);
            Assert.AreEqual(PackageIndexingResult.Success,
                await scope.ServiceProvider.GetRequiredService<IPackageIndexingService>().IndexAsync(stream,
                    new PackagePublisher(owner.Id, true, true), CancellationToken.None));
        }
        AddArchive("Company.Personal");
        Assert.AreEqual(0, await SynchronizeAsync());
        using var verification = Server!.Services.CreateScope();
        var versions = await verification.ServiceProvider.GetRequiredService<NupkgDbContext>().Packages
            .Where(p => p.Id == "company.personal").ToListAsync();
        Assert.HasCount(1, versions);
        Assert.AreEqual("1.0.0", versions.Single().NormalizedVersionString);
        Assert.IsTrue(versions.Single().IsLocallyPublished);
    }

    [TestMethod]
    public async Task UncachedApprovedVersionsAndRegistrationAreVisibleBeforeAnyArchiveDownload()
    {
        var bytes = AddArchive("Lazy.Metadata");
        Assert.Contains("1.2.3", await Http.GetStringAsync("/v3/package/lazy.metadata/index.json"));
        var registration = await Http.GetStringAsync("/v3/registration/lazy.metadata/index.json");
        Assert.Contains("1.2.3", registration);
        Assert.DoesNotContain("9.9.9", registration);
        Assert.AreEqual(HttpStatusCode.OK, (await Http.GetAsync("/v3/registration/lazy.metadata/1.2.3.json")).StatusCode);
        Assert.AreEqual(0, _downloads);
        using (var scope = Server!.Services.CreateScope())
            Assert.IsFalse(await scope.ServiceProvider.GetRequiredService<NupkgDbContext>().Packages.AnyAsync(p => p.Id == "lazy.metadata"));
        CollectionAssert.AreEqual(bytes, await Http.GetByteArrayAsync("/v3/package/lazy.metadata/1.2.3/lazy.metadata.1.2.3.nupkg"));
        CollectionAssert.AreEqual(bytes, await Http.GetByteArrayAsync("/v3/package/lazy.metadata/1.2.3/lazy.metadata.1.2.3.nupkg"));
        Assert.AreEqual(1, _downloads);
    }

    [TestMethod]
    public async Task CachedMetadataAndContentRemainAvailableWhenTheOriginIsOffline()
    {
        var bytes = AddArchive("Lazy.CachedOffline");
        var url = "/v3/package/lazy.cachedoffline/1.2.3/lazy.cachedoffline.1.2.3.nupkg";
        CollectionAssert.AreEqual(bytes, await Http.GetByteArrayAsync(url));
        await _upstream!.StopAsync();
        Assert.Contains("1.2.3", await Http.GetStringAsync("/v3/package/lazy.cachedoffline/index.json"));
        Assert.AreEqual(HttpStatusCode.OK, (await Http.GetAsync("/v3/registration/lazy.cachedoffline/index.json")).StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, (await Http.GetAsync("/v3/registration/lazy.cachedoffline/1.2.3.json")).StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, (await Http.GetAsync("/packages/lazy.cachedoffline/1.2.3")).StatusCode);
        CollectionAssert.AreEqual(bytes, await Http.GetByteArrayAsync(url));
        Assert.AreEqual(1, _downloads);
    }

    [TestMethod]
    public async Task ConcurrentColdDownloadsShareOneVerifiedArchive()
    {
        var bytes = AddArchive("Lazy.Concurrent");
        _downloadStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _downloadRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = Http.GetByteArrayAsync("/v3/package/lazy.concurrent/1.2.3/lazy.concurrent.1.2.3.nupkg");
        await _downloadStarted.Task.WaitAsync(TimeSpan.FromSeconds(15));
        var others = Enumerable.Range(0, 7).Select(_ =>
            Http.GetByteArrayAsync("/v3/package/lazy.concurrent/1.2.3/lazy.concurrent.1.2.3.nupkg")).ToArray();
        Assert.IsFalse(first.IsCompleted, "Content must wait for the complete verified archive.");
        _downloadRelease.SetResult(true);
        foreach (var result in await Task.WhenAll(others.Prepend(first))) CollectionAssert.AreEqual(bytes, result);
        Assert.AreEqual(1, _downloads);
    }

    [TestMethod]
    public async Task RevocationDuringColdDownloadDoesNotCacheOrReturnTheArchive()
    {
        AddArchive("Lazy.Revoked");
        _downloadStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _downloadRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var download = Http.GetAsync("/v3/package/lazy.revoked/1.2.3/lazy.revoked.1.2.3.nupkg");
        await _downloadStarted.Task.WaitAsync(TimeSpan.FromSeconds(15));
        _revoked.Add("lazy.revoked");
        _downloadRelease.SetResult(true);
        Assert.AreEqual(HttpStatusCode.Forbidden, (await download).StatusCode);
        using var scope = Server!.Services.CreateScope();
        Assert.IsFalse(await scope.ServiceProvider.GetRequiredService<NupkgDbContext>().Packages.AnyAsync(p => p.Id == "lazy.revoked"));
    }

    [TestMethod]
    public async Task FailedColdVerificationLeavesNoPackageAndCanBeRetried()
    {
        var bytes = AddArchive("Lazy.Tampered");
        _archives["lazy.tampered"] = Encoding.UTF8.GetBytes("not the approved archive");
        var url = "/v3/package/lazy.tampered/1.2.3/lazy.tampered.1.2.3.nupkg";
        Assert.AreEqual(HttpStatusCode.BadGateway, (await Http.GetAsync(url)).StatusCode);
        using (var scope = Server!.Services.CreateScope())
            Assert.IsFalse(await scope.ServiceProvider.GetRequiredService<NupkgDbContext>().Packages.AnyAsync(p => p.Id == "lazy.tampered"));
        _archives["lazy.tampered"] = bytes;
        CollectionAssert.AreEqual(bytes, await Http.GetByteArrayAsync(url));
        Assert.AreEqual(2, _downloads);
    }

    [TestMethod]
    public async Task InterruptedColdDownloadCanBeRetriedWithoutPublishingPartialContent()
    {
        var bytes = AddArchive("Lazy.Interrupted");
        _downloadStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        _downloadRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        var url = "/v3/package/lazy.interrupted/1.2.3/lazy.interrupted.1.2.3.nupkg";
        var interrupted = Http.GetAsync(url, cancellation.Token);
        await _downloadStarted.Task.WaitAsync(TimeSpan.FromSeconds(15));
        await cancellation.CancelAsync();
        var cancelled = false;
        try { await interrupted; }
        catch (OperationCanceledException) { cancelled = true; }
        Assert.IsTrue(cancelled);
        _downloadRelease.SetResult(true);
        CollectionAssert.AreEqual(bytes, await Http.GetByteArrayAsync(url));
        Assert.AreEqual(2, _downloads);
        using var scope = Server!.Services.CreateScope();
        Assert.AreEqual(1, await scope.ServiceProvider.GetRequiredService<NupkgDbContext>().Packages.CountAsync(p => p.Id == "lazy.interrupted"));
    }

    [TestMethod]
    public async Task ColdMetadataAndContentFailClosedWhenAdmissionIsUnavailable()
    {
        AddArchive("Lazy.Unavailable");
        _unavailable = true;
        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, (await Http.GetAsync("/v3/package/lazy.unavailable/index.json")).StatusCode);
        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, (await Http.GetAsync("/v3/registration/lazy.unavailable/index.json")).StatusCode);
        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, (await Http.GetAsync("/v3/package/lazy.unavailable/1.2.3/lazy.unavailable.1.2.3.nupkg")).StatusCode);
        Assert.AreEqual(0, _downloads);
    }

    [TestMethod]
    public async Task UnavailableOrMismatchedAdmissionFailsClosedWithoutBreakingHealthOrServiceIndex()
    {
        AddArchive("Mirror.Available");
        await SynchronizeAsync();
        _unavailable = true;
        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, (await Http.GetAsync("/v3/package/mirror.available/index.json")).StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, (await Http.GetAsync("/health")).StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, (await Http.GetAsync("/upload")).StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, (await Http.GetAsync("/v3/index.json")).StatusCode);
        _unavailable = false;
        _mismatchedResponse = true;
        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, (await Http.GetAsync("/v3/package/mirror.available/index.json")).StatusCode);
        Assert.AreEqual(1, _downloads);
    }
}
