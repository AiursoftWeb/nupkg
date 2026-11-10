using System.Net;
using System.Text.Json;
using Aiursoft.Nupkg.Entities;
using NuGet.Versioning;

namespace Aiursoft.Nupkg.Tests.IntegrationTests;

[TestClass]
public class CatalogDetailsTests : TestBase
{
    [TestMethod]
    public async Task DetailsExposePackageMetadataAndAllLibraryInstallationFormats()
    {
        var id = NewId();
        var package = Version(id);
        package.HasEmbeddedIcon = true;
        package.ProjectUrl = new Uri("https://example.test/project");
        package.RepositoryUrl = new Uri("https://example.test/source");
        package.LicenseUrl = new Uri("https://example.test/license");
        await SeedFamilyAsync(package);
        var html = await Http.GetStringAsync($"/packages/{id}/1.0.0");
        Assert.Contains($"/v3/package/{id}/1.0.0/icon", html);
        Assert.Contains("https://example.test/project", html);
        Assert.Contains("https://example.test/source", html);
        Assert.Contains("https://example.test/license", html);
        Assert.Contains("net10.0", html);
        Assert.Contains("Last updated", html);
        Assert.Contains($"dotnet add package {id} --version 1.0.0 --source {Http.BaseAddress}v3/index.json", html);
        Assert.Contains($"Install-Package {id} -Version 1.0.0 -Source {Http.BaseAddress}v3/index.json", html);
        Assert.Contains($"paket add {id} --version 1.0.0", html);
        Assert.Contains($"&lt;PackageReference Include=&quot;{id}&quot; Version=&quot;1.0.0&quot; /&gt;", html);
    }

    [TestMethod]
    public async Task UnsafeMetadataLinksAndIconsAreNotRenderedAsActiveUrls()
    {
        var id = NewId();
        var package = Version(id);
        package.IconUrl = new Uri("javascript:alert('icon')");
        package.RepositoryUrl = new Uri("javascript:alert('source')");
        package.ProjectUrl = new Uri("data:text/html,unsafe");
        package.LicenseUrl = new Uri("file:///fixture-license");
        await SeedFamilyAsync(package);
        var html = await Http.GetStringAsync($"/packages/{id}");
        Assert.DoesNotContain("javascript:", html);
        Assert.DoesNotContain("data:text/html", html);
        Assert.DoesNotContain("file:///fixture-license", html);
        var catalog = await Http.GetStringAsync($"/packages?q={id}");
        Assert.DoesNotContain("javascript:", catalog);
    }

    [TestMethod]
    public async Task ToolsAndTemplatesUseTheirOwnInstallationCommands()
    {
        foreach (var type in new[] { "DotnetTool", "Template" })
        {
            var id = NewId();
            var package = Version(id);
            package.PackageTypes = [new PackageType { Name = type, Version = "0.0" }];
            await SeedFamilyAsync(package);
            var html = await Http.GetStringAsync($"/packages/{id}");
            Assert.Contains(type == "DotnetTool"
                ? $"dotnet tool install --global {id} --version 1.0.0 --add-source {Http.BaseAddress}v3/index.json"
                : $"dotnet new install {id}@1.0.0 --add-source {Http.BaseAddress}v3/index.json", html);
            if (type == "DotnetTool") Assert.DoesNotContain("id=\"install-format\"", html);
            else
            {
                Assert.Contains("Earlier SDKs", html);
                Assert.Contains($"dotnet new install {id}::1.0.0 --nuget-source {Http.BaseAddress}v3/index.json", html);
            }
        }
    }

    [TestMethod]
    public async Task DependentsCountDistinctNamesBeforePagingAndRetainCatalogContext()
    {
        var id = NewId();
        await SeedFamilyAsync(Version(id));
        for (var n = 0; n < 25; n++)
        {
            var name = $"{id}.consumer.{n:D2}";
            var first = Version(name);
            var second = Version(name, "2.0.0");
            first.Dependencies = [new PackageDependency { Id = id.ToUpperInvariant(), VersionRange = "[1.0.0,)", TargetFramework = "net10.0" }];
            second.Dependencies = [new PackageDependency { Id = id, VersionRange = "[1.0.0,)", TargetFramework = "net10.0" }];
            await SeedFamilyAsync(first, second);
        }
        var unlisted = Version(id + ".hidden");
        unlisted.Listed = false;
        unlisted.Dependencies = [new PackageDependency { Id = id, VersionRange = "[1.0.0,)", TargetFramework = "net10.0" }];
        await SeedFamilyAsync(unlisted);
        using var firstPage = JsonDocument.Parse(await Http.GetStringAsync($"/v3/dependents?packageId={id.ToUpperInvariant()}"));
        using var secondPage = JsonDocument.Parse(await Http.GetStringAsync($"/v3/dependents?packageId={id}&skip=20&take=20"));
        Assert.AreEqual(25, firstPage.RootElement.GetProperty("totalHits").GetInt32());
        Assert.AreEqual(20, firstPage.RootElement.GetProperty("data").GetArrayLength());
        Assert.AreEqual(25, secondPage.RootElement.GetProperty("totalHits").GetInt32());
        Assert.AreEqual(5, secondPage.RootElement.GetProperty("data").GetArrayLength());
        var names = firstPage.RootElement.GetProperty("data").EnumerateArray().Concat(secondPage.RootElement.GetProperty("data").EnumerateArray()).Select(p => p.GetProperty("id").GetString()).ToList();
        Assert.AreEqual(25, names.Distinct().Count());
        Assert.IsFalse(names.Contains(unlisted.Id));
        var html = WebUtility.HtmlDecode(await Http.GetStringAsync($"/packages/{id}/1.0.0?tab=usedby&dependentsPage=2&q=fixture&page=3&framework=net10.0&prerelease=false"));
        Assert.Contains("Page 2 of 2", html);
        Assert.Contains("q=fixture", html);
        Assert.Contains("page=3", html);
        Assert.Contains("framework=net10.0", html);
        Assert.Contains("prerelease=False", html);
        Assert.Contains("tab=usedby", html);
        Assert.Contains("#package-dependents", html);
        Assert.Contains("show active\" id=\"package-dependents\"", html);
    }

    private async Task SeedFamilyAsync(params Package[] packages)
    {
        using var scope = Server!.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NupkgDbContext>();
        var family = new PackageFamily
        {
            PackageId = packages[0].Id, NormalizedId = packages[0].Id.ToLowerInvariant(), OwnerUserId = null
        };
        db.PackageFamilies.Add(family);
        foreach (var package in packages)
        {
            package.PackageFamily = family;
        }
        db.Packages.AddRange(packages);
        await db.SaveChangesAsync();
    }

    private static Package Version(string id, string version = "1.0.0") => new()
    {
        Id = id, Version = NuGetVersion.Parse(version), Listed = true, Description = "Catalog fixture.",
        Authors = ["Catalog fixture"], Tags = ["catalog"], Published = DateTime.UtcNow,
        Dependencies = [], PackageTypes = [new PackageType { Name = "Dependency", Version = "0.0" }],
        TargetFrameworks = [new TargetFramework { Moniker = "net10.0" }]
    };

    private static string NewId() => "catalog." + Guid.NewGuid().ToString("N");
}
