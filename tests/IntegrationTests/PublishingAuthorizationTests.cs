using System.IO.Compression;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using Aiursoft.Nupkg.Authorization;
using Aiursoft.Nupkg.Entities;
using Aiursoft.Nupkg.Services.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.Nupkg.Tests.IntegrationTests;

[TestClass]
public class PublishingAuthorizationTests : TestBase
{
    private IConfiguration Configuration => Server!.Services.GetRequiredService<IConfiguration>();

    [TestMethod]
    public async Task AnonymousWritesRequireExplicitCompatibilityWhileDownloadsRemainPublic()
    {
        Configuration["ApiKey"] = string.Empty;
        var id = NewId();
        var bytes = PackageBytes(id);
        Assert.AreEqual(HttpStatusCode.Unauthorized, await SendAsync(HttpMethod.Put, "/api/v2/package", null, bytes));
        Configuration["AllowAnonymousPublishing"] = "true";
        Assert.AreEqual(HttpStatusCode.Created, await SendAsync(HttpMethod.Put, "/api/v2/package", null, bytes));
        Configuration["AllowAnonymousPublishing"] = "false";
        CollectionAssert.AreEqual(bytes, await Http.GetByteArrayAsync($"/v3/package/{id}/1.0.0/{id}.1.0.0.nupkg"));
        using var scope = Server!.Services.CreateScope();
        var family = await scope.ServiceProvider.GetRequiredService<NupkgDbContext>().PackageFamilies.SingleAsync(f => f.NormalizedId == id);
        Assert.IsNull(family.OwnerUserId);
    }

    [TestMethod]
    public async Task PersonalPublisherOwnsTheWholeNameIncludingSymbolsAndUnlistedVersions()
    {
        var owner = await CreatePublisherAsync();
        var other = await CreatePublisherAsync();
        var id = NewId();
        var bytes = PackageBytes(id);
        Assert.AreEqual(HttpStatusCode.Created, await SendAsync(HttpMethod.Put, "/api/v2/package", owner.Key, bytes));
        Assert.AreEqual(HttpStatusCode.Forbidden, await SendAsync(HttpMethod.Put, "/api/v2/package", other.Key, PackageBytes(id, "1.0.1")));
        Assert.AreEqual(HttpStatusCode.Forbidden, await SendAsync(HttpMethod.Delete, $"/api/v2/package/{id}/1.0.0", other.Key));
        Assert.AreEqual(HttpStatusCode.Forbidden, await SendAsync(HttpMethod.Post, $"/api/v2/package/{id}/1.0.0", other.Key));
        Assert.AreEqual(HttpStatusCode.Forbidden, await SendAsync(HttpMethod.Put, "/api/v2/symbol", other.Key, PackageBytes(id, symbols: true)));
        Assert.AreEqual(HttpStatusCode.Created, await SendAsync(HttpMethod.Put, "/api/v2/symbol", owner.Key, PackageBytes(id, symbols: true)));
        Assert.AreEqual(HttpStatusCode.NoContent, await SendAsync(HttpMethod.Delete, $"/api/v2/package/{id}/1.0.0", owner.Key));
        Assert.AreEqual(HttpStatusCode.OK, await SendAsync(HttpMethod.Post, $"/api/v2/package/{id}/1.0.0", owner.Key));
        CollectionAssert.AreEqual(bytes, await Http.GetByteArrayAsync($"/v3/package/{id}/1.0.0/{id}.1.0.0.nupkg"));
        using var scope = Server!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NupkgDbContext>();
        var family = await db.PackageFamilies.SingleAsync(f => f.NormalizedId == id);
        Assert.AreEqual(owner.UserId, family.OwnerUserId);
        Assert.AreEqual(owner.UserId, (await db.Packages.SingleAsync(v => v.Id == id)).UploadedByUserId);
    }

    [TestMethod]
    public async Task AccountPermissionChangesExpirationAndRevocationApplyToExistingKeys()
    {
        var publisher = await CreatePublisherAsync(publish: false);
        Assert.AreEqual(HttpStatusCode.Forbidden, await SendAsync(HttpMethod.Put, "/api/v2/package", publisher.Key, PackageBytes(NewId())));
        await SignInAsync(publisher);
        var usageUrl = $"/UserApiKeys/Usage/{publisher.KeyId}";
        Assert.Contains("Publishing permission is required.", await Http.GetStringAsync(usageUrl));
        using (var scope = Server!.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = (await manager.FindByIdAsync(publisher.UserId))!;
            Assert.IsTrue((await manager.AddClaimAsync(user, new Claim(AppPermissions.Type, AppPermissionNames.CanPublishPackages))).Succeeded);
        }
        Assert.AreEqual(HttpStatusCode.Created, await SendAsync(HttpMethod.Put, "/api/v2/package", publisher.Key, PackageBytes(NewId())));
        Assert.DoesNotContain("Publishing permission is required.", await Http.GetStringAsync(usageUrl));
        using (var scope = Server!.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var user = (await manager.FindByIdAsync(publisher.UserId))!;
            var permission = new Claim(AppPermissions.Type, AppPermissionNames.CanPublishPackages);
            Assert.IsTrue((await manager.RemoveClaimAsync(user, permission)).Succeeded);
        }
        Assert.AreEqual(HttpStatusCode.Forbidden, await SendAsync(HttpMethod.Put, "/api/v2/package", publisher.Key, PackageBytes(NewId())));
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NupkgDbContext>();
            var key = await db.UserApiKeys.SingleAsync(k => k.Id == publisher.KeyId);
            Assert.IsNotNull(key.LastUsedAt);
            key.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
            await db.SaveChangesAsync();
        }
        Assert.AreEqual(HttpStatusCode.Unauthorized, await SendAsync(HttpMethod.Put, "/api/v2/package", publisher.Key, PackageBytes(NewId())));
        using (var scope = Server!.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NupkgDbContext>();
            db.UserApiKeys.Remove(await db.UserApiKeys.SingleAsync(k => k.Id == publisher.KeyId));
            await db.SaveChangesAsync();
        }
        Assert.AreEqual(HttpStatusCode.Unauthorized, await SendAsync(HttpMethod.Put, "/api/v2/package", publisher.Key, PackageBytes(NewId())));
    }

    [TestMethod]
    public async Task LegacyKeyPreservesExistingAutomationAndNamesCannotBeClaimedByOrdinaryPublishers()
    {
        Configuration["ApiKey"] = "legacy-publishing-fixture";
        var publisher = await CreatePublisherAsync();
        var id = NewId();
        Assert.AreEqual(HttpStatusCode.Created, await SendAsync(HttpMethod.Put, "/api/v2/package", "legacy-publishing-fixture", PackageBytes(id)));
        Assert.AreEqual(HttpStatusCode.Forbidden, await SendAsync(HttpMethod.Put, "/api/v2/package", publisher.Key, PackageBytes(id, "1.0.1")));
        Assert.AreEqual(HttpStatusCode.Created, await SendAsync(HttpMethod.Put, "/api/v2/package", "legacy-publishing-fixture", PackageBytes(id, "1.0.1")));
        Configuration["EnableLegacyApiKey"] = "false";
        Assert.AreEqual(HttpStatusCode.Unauthorized, await SendAsync(HttpMethod.Put, "/api/v2/package", "legacy-publishing-fixture", PackageBytes(NewId())));
        Assert.AreEqual(HttpStatusCode.Created, await SendAsync(HttpMethod.Put, "/api/v2/package", publisher.Key, PackageBytes(NewId())));
    }

    [TestMethod]
    public async Task RemovingTheLastVersionDoesNotReleaseItsOwnerReservation()
    {
        Configuration["PackageDeletionBehavior"] = "HardDelete";
        var owner = await CreatePublisherAsync();
        var other = await CreatePublisherAsync();
        var id = NewId();
        Assert.AreEqual(HttpStatusCode.Created, await SendAsync(HttpMethod.Put, "/api/v2/package", owner.Key, PackageBytes(id)));
        Assert.AreEqual(HttpStatusCode.NoContent, await SendAsync(HttpMethod.Delete, $"/api/v2/package/{id}/1.0.0", owner.Key));
        Assert.AreEqual(HttpStatusCode.Forbidden, await SendAsync(HttpMethod.Put, "/api/v2/package", other.Key, PackageBytes(id, "1.0.1")));
        Assert.AreEqual(HttpStatusCode.Created, await SendAsync(HttpMethod.Put, "/api/v2/package", owner.Key, PackageBytes(id, "1.0.1")));
    }

    [TestMethod]
    public async Task PersonalKeyIsShownOnceStoredAsHashAndCanOnlyBeRevokedByItsUser()
    {
        var user = await CreatePublisherAsync();
        await SignInAsync(user);
        var name = "UI key " + Guid.NewGuid().ToString("N");
        using var created = await PostForm("/UserApiKeys/Create", new Dictionary<string, string> { ["Name"] = name, ["ExpirationDays"] = "30" });
        Assert.AreEqual(HttpStatusCode.Found, created.StatusCode);
        var url = created.Headers.Location!.ToString();
        var html = await Http.GetStringAsync(url);
        var raw = Regex.Match(html, "id=\"new-api-key\"[^>]*>([^<]+)</code>").Groups[1].Value;
        Assert.AreEqual(43, raw.Length);
        Assert.DoesNotContain("id=\"new-api-key\"", await Http.GetStringAsync(url));
        int id;
        using (var scope = Server!.Services.CreateScope())
        {
            var key = await scope.ServiceProvider.GetRequiredService<NupkgDbContext>().UserApiKeys.SingleAsync(k => k.Name == name);
            id = key.Id;
            Assert.AreEqual(PackagePublisherAuthenticationService.ComputeSha256Hex(raw), key.KeyHash);
            Assert.AreEqual(raw[..8], key.KeyPrefix);
        }
        var other = await CreatePublisherAsync();
        await SignInAsync(other);
        Assert.AreEqual(HttpStatusCode.NotFound, (await Http.GetAsync(url)).StatusCode);
        var rejected = await PostForm($"/UserApiKeys/Delete/{id}", [], tokenUrl: "/UserApiKeys/Create");
        Assert.AreEqual(HttpStatusCode.NotFound, rejected.StatusCode);
        await SignInAsync(user);
        var revoked = await PostForm($"/UserApiKeys/Delete/{id}", [], tokenUrl: $"/UserApiKeys/Delete/{id}");
        Assert.AreEqual(HttpStatusCode.Found, revoked.StatusCode);
        Assert.AreEqual(HttpStatusCode.Unauthorized, await SendAsync(HttpMethod.Put, "/api/v2/package", raw, PackageBytes(NewId())));
    }

    [TestMethod]
    public async Task AdministratorAssignsLegacyOwnershipAndStaleAssignmentCannotOverwriteIt()
    {
        Configuration["ApiKey"] = "ownership-legacy-fixture";
        var owner = await CreatePublisherAsync();
        var other = await CreatePublisherAsync();
        var administrator = await CreatePublisherAsync(manage: true);
        var id = NewId();
        Assert.AreEqual(HttpStatusCode.Created, await SendAsync(HttpMethod.Put, "/api/v2/package", "ownership-legacy-fixture", PackageBytes(id)));
        int familyId;
        using (var scope = Server!.Services.CreateScope())
            familyId = await scope.ServiceProvider.GetRequiredService<NupkgDbContext>().PackageFamilies.Where(f => f.NormalizedId == id).Select(f => f.Id).SingleAsync();
        await SignInAsync(owner);
        Assert.AreEqual(HttpStatusCode.Redirect, (await Http.GetAsync("/PackageOwnership/Index")).StatusCode);
        await SignInAsync(administrator);
        var url = $"/PackageOwnership/Assign/{familyId}";
        var assigned = await PostForm(url, new Dictionary<string, string>
        {
            ["OwnerUserId"] = owner.UserId, ["PreviousOwnerUserId"] = string.Empty,
            ["Query"] = id, ["Page"] = "2", ["UnassignedOnly"] = "true"
        });
        Assert.AreEqual(HttpStatusCode.Found, assigned.StatusCode);
        Assert.Contains("page=2", assigned.Headers.Location!.ToString().ToLowerInvariant());
        Assert.AreEqual(HttpStatusCode.Created, await SendAsync(HttpMethod.Put, "/api/v2/package", owner.Key, PackageBytes(id, "1.0.1")));
        var stale = await PostForm(url, new Dictionary<string, string> { ["OwnerUserId"] = other.UserId, ["PreviousOwnerUserId"] = string.Empty });
        Assert.AreEqual(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.AreEqual(HttpStatusCode.Forbidden, await SendAsync(HttpMethod.Put, "/api/v2/package", other.Key, PackageBytes(id, "1.0.2")));
    }

    private sealed record Publisher(string UserId, string Email, string Key, int KeyId);

    private async Task<Publisher> CreatePublisherAsync(bool publish = true, bool manage = false)
    {
        using var scope = Server!.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var email = "publisher-" + Guid.NewGuid().ToString("N") + "@example.test";
        var user = new User { UserName = email, Email = email, DisplayName = "Publisher" };
        Assert.IsTrue((await users.CreateAsync(user, "Publisher-Test-123!")).Succeeded);
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var role = new IdentityRole("Publishers-" + Guid.NewGuid().ToString("N"));
        Assert.IsTrue((await roles.CreateAsync(role)).Succeeded);
        if (publish) Assert.IsTrue((await roles.AddClaimAsync(role, new Claim(AppPermissions.Type, AppPermissionNames.CanPublishPackages))).Succeeded);
        if (manage) Assert.IsTrue((await roles.AddClaimAsync(role, new Claim(AppPermissions.Type, AppPermissionNames.CanManagePackages))).Succeeded);
        Assert.IsTrue((await users.AddToRoleAsync(user, role.Name!)).Succeeded);
        var raw = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        var key = new UserApiKey { UserId = user.Id, Name = "Fixture", KeyHash = PackagePublisherAuthenticationService.ComputeSha256Hex(raw), KeyPrefix = raw[..8] };
        var db = scope.ServiceProvider.GetRequiredService<NupkgDbContext>();
        db.UserApiKeys.Add(key);
        await db.SaveChangesAsync();
        return new Publisher(user.Id, email, raw, key.Id);
    }

    private async Task SignInAsync(Publisher user)
    {
        var response = await PostForm("/Account/Login", new Dictionary<string, string> { ["EmailOrUserName"] = user.Email, ["Password"] = "Publisher-Test-123!" });
        Assert.AreEqual(HttpStatusCode.Found, response.StatusCode);
    }

    private async Task<HttpStatusCode> SendAsync(HttpMethod method, string url, string? key, byte[]? content = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (key != null) request.Headers.Add("X-NuGet-ApiKey", key);
        if (content != null) request.Content = new ByteArrayContent(content);
        using var response = await Http.SendAsync(request);
        return response.StatusCode;
    }

    private static string NewId() => "nupkg.publisher." + Guid.NewGuid().ToString("N");

    private static byte[] PackageBytes(string id, string version = "1.0.0", bool symbols = false)
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, true))
        {
            using (var writer = new StreamWriter(archive.CreateEntry(id + ".nuspec").Open(), Encoding.UTF8))
                writer.Write($"<?xml version=\"1.0\"?><package xmlns=\"http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd\"><metadata><id>{id}</id><version>{version}</version><authors>Publisher fixture</authors><description>Publishing authorization fixture.</description></metadata></package>");
            if (symbols)
            {
                var pdb = Path.ChangeExtension(typeof(PublishingAuthorizationTests).Assembly.Location, ".pdb");
                using var input = File.OpenRead(pdb);
                using var target = archive.CreateEntry("lib/net10.0/fixture.pdb").Open();
                input.CopyTo(target);
            }
        }
        return buffer.ToArray();
    }
}
