using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Aiursoft.Nupkg.Core.Configuration;
using Aiursoft.Nupkg.Core.Extensions;
using Aiursoft.Nupkg.Protocol;

namespace Aiursoft.Nupkg.Tests.IntegrationTests;

[TestClass]
public class MirrorCompatibilityTests
{
    [TestMethod]
    [DataRow("gzip")]
    [DataRow("deflate")]
    public async Task MirrorReadsCompressedUpstreamMetadataAlongsideTemplateHttpClients(string encoding)
    {
        using var reservation = new TestPortReservation();
        var origin = $"http://127.0.0.1:{reservation.Port}";
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://0.0.0.0:{reservation.Port}");
        await using var upstream = builder.Build();
        reservation.ConfigureServer(upstream);
        var resources = new[]
        {
            ("PackageBaseAddress/3.0.0", "/flat/"),
            ("RegistrationsBaseUrl/3.6.0", "/registration/"),
            ("SearchQueryService/3.4.0", "/search")
        }.Select(r => new Dictionary<string, string> { ["@type"] = r.Item1, ["@id"] = origin + r.Item2 });
        var serviceIndex = JsonSerializer.Serialize(new { version = "3.0.0", resources });
        const string registration = """
            {"count":1,"items":[{"count":1,"lower":"1.2.3","upper":"1.2.3","items":[
              {"catalogEntry":{"id":"Compression.Package","version":"1.2.3","listed":true}}
            ]}]}
            """;
        upstream.Run(async context =>
        {
            context.Response.ContentType = "application/json";
            context.Response.Headers.ContentEncoding = encoding;
            await using Stream compressed = encoding == "gzip"
                ? new GZipStream(context.Response.Body, CompressionMode.Compress, leaveOpen: true)
                : new DeflateStream(context.Response.Body, CompressionMode.Compress, leaveOpen: true);
            var json = context.Request.Path == "/v3/index.json" ? serviceIndex : registration;
            await compressed.WriteAsync(Encoding.UTF8.GetBytes(json));
        });
        await upstream.StartAsync();
        try
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddHttpClient();
            services.Configure<MirrorOptions>(options =>
            {
                options.Enabled = true;
                options.PackageSource = new Uri(origin + "/v3/index.json");
            });
            services.AddNupkgServices();
            await using var provider = services.BuildServiceProvider();
            var client = provider.GetRequiredService<NuGetClient>();
            var packages = await client.GetPackageMetadataAsync("Compression.Package");
            Assert.HasCount(1, packages);
            Assert.AreEqual("Compression.Package", packages[0].PackageId);
            Assert.AreEqual("1.2.3", packages[0].Version);
        }
        finally
        {
            await upstream.StopAsync();
        }
    }
}
