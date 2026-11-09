using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Aiursoft.Nupkg.Tests.IntegrationTests;

[TestClass]
public class NuGetCompatibilityTests : TestBase
{
    [TestInitialize]
    public void ConfigureLegacyFixture() => Server!.Services.GetRequiredService<IConfiguration>()["ApiKey"] = "nupkg-compatibility-fixture";

    private string ApiKey => Server!.Services.GetRequiredService<IConfiguration>()["ApiKey"]!;

    [TestMethod]
    public async Task ServiceIndexRetainsNuGetJsonContract()
    {
        using var response = await Http.GetAsync("/v3/index.json");
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.AreEqual("3.0.0", json.RootElement.GetProperty("version").GetString());
        var resources = json.RootElement.GetProperty("resources").EnumerateArray().ToList();
        Assert.IsTrue(resources.Any(r => r.GetProperty("@type").GetString() == "PackageBaseAddress/3.0.0"));
        Assert.IsTrue(resources.All(r => r.GetProperty("@id").GetString()!.StartsWith(Http.BaseAddress!.ToString(), StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task PackagePushSearchDownloadAndUnlistRetainTheirContracts()
    {
        var id = $"nupkg.compatibility.{Guid.NewGuid():N}";
        var bytes = PackageBytes(id);
        using var push = new HttpRequestMessage(HttpMethod.Put, "/api/v2/package");
        push.Headers.Add("X-NuGet-ApiKey", ApiKey);
        push.Content = new ByteArrayContent(bytes);
        using var pushed = await Http.SendAsync(push);
        Assert.AreEqual(HttpStatusCode.Created, pushed.StatusCode);

        using var search = JsonDocument.Parse(await Http.GetStringAsync($"/v3/search?q={id}&semVerLevel=2.0.0"));
        Assert.AreEqual(1, search.RootElement.GetProperty("totalHits").GetInt32());
        Assert.AreEqual(id, search.RootElement.GetProperty("data")[0].GetProperty("id").GetString());
        var lower = id.ToLowerInvariant();
        CollectionAssert.AreEqual(bytes, await Http.GetByteArrayAsync($"/v3/package/{lower}/1.2.3/{lower}.1.2.3.nupkg"));
        var page = await Http.GetStringAsync($"/packages/{id}/1.2.3");
        Assert.Contains(id, page);
        Assert.Contains("Listed", page);
        Assert.Contains("Package readme", page);
        Assert.DoesNotContain("<script>alert(", page);

        using var delete = new HttpRequestMessage(HttpMethod.Delete, $"/api/v2/package/{id}/1.2.3");
        delete.Headers.Add("X-NuGet-ApiKey", ApiKey);
        using var deleted = await Http.SendAsync(delete);
        Assert.AreEqual(HttpStatusCode.NoContent, deleted.StatusCode);
        using var after = JsonDocument.Parse(await Http.GetStringAsync($"/v3/search?q={id}&semVerLevel=2.0.0"));
        Assert.AreEqual(0, after.RootElement.GetProperty("totalHits").GetInt32());
        Assert.Contains("Unlisted", await Http.GetStringAsync($"/packages/{id}/1.2.3"));
        CollectionAssert.AreEqual(bytes, await Http.GetByteArrayAsync($"/v3/package/{lower}/1.2.3/{lower}.1.2.3.nupkg"));
    }

    [TestMethod]
    public async Task BrowserSignInDoesNotReplacePublishingApiKey()
    {
        await LoginAsAdmin();
        using var push = new HttpRequestMessage(HttpMethod.Put, "/api/v2/package");
        push.Headers.Add("X-NuGet-ApiKey", "incorrect-fixture-key");
        push.Content = new ByteArrayContent([]);
        using var response = await Http.SendAsync(push);
        Assert.AreEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.IsNull(response.Headers.Location);
        Assert.DoesNotContain("<!DOCTYPE", await response.Content.ReadAsStringAsync());
    }

    [TestMethod]
    public async Task CatalogAndPublishingInstructionsArePublicAndUseTemplateLayout()
    {
        var home = await Http.GetStringAsync("/");
        using var scope = Server!.Services.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<Services.GlobalSettingsService>();
        var projectName = await settings.GetSettingValueAsync(Configuration.SettingsMap.ProjectName);
        Assert.Contains(projectName, home);
        Assert.Contains("Search packages", home);
        Assert.Contains("sidebar", home);
        (await Http.GetAsync("/Catalog/Index")).EnsureSuccessStatusCode();
        (await Http.GetAsync("/Catalog/Upload")).EnsureSuccessStatusCode();
        var upload = await Http.GetStringAsync("/upload");
        Assert.Contains("YOUR_API_KEY", upload);
        Assert.Contains("/v3/index.json", upload);
        Assert.DoesNotContain(ApiKey, upload);
    }

    private static byte[] PackageBytes(string id)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, true))
        {
            using (var writer = new StreamWriter(zip.CreateEntry($"{id}.nuspec").Open(), Encoding.UTF8))
            {
                writer.Write($"<?xml version=\"1.0\"?><package xmlns=\"http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd\"><metadata><id>{id}</id><version>1.2.3</version><authors>Nupkg test</authors><description>Package compatibility fixture.</description><readme>readme.md</readme></metadata></package>");
            }
            using var readme = new StreamWriter(zip.CreateEntry("readme.md").Open(), Encoding.UTF8);
            readme.Write("# Package readme\n<script>alert('fixture');</script>\n");
        }
        return buffer.ToArray();
    }
}
