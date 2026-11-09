using Aiursoft.Nupkg.Core;
using Aiursoft.Nupkg.Core.Content;
using Aiursoft.Nupkg.Core.Search;
using Aiursoft.Nupkg.Models.PackagesViewModels;
using Aiursoft.Nupkg.Services;
using Aiursoft.UiStack.Navigation;
using Aiursoft.WebTools.Attributes;
using Markdig;
using Microsoft.AspNetCore.Mvc;
using NuGet.Versioning;
using Aiursoft.Nupkg.Entities;
using Microsoft.EntityFrameworkCore;
namespace Aiursoft.Nupkg.Controllers;

[LimitPerMin]
public class CatalogController(ISearchService search, IPackageService packages,
    IPackageContentService content, IUrlGenerator urls, IConfiguration configuration, NupkgDbContext db) : Controller
{
    private static readonly MarkdownPipeline ReadmePipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().DisableHtml().Build();

    [HttpGet("/packages", Name = "PackageCatalog")]
    [HttpGet("/Catalog/Index")]
    [HttpGet("/")]
    [HttpGet("/Home")]
    [HttpGet("/Home/Index")]
    [HttpGet("/Dashboard/Index")]
    [HttpGet("/Dashboard")]
    [RenderInNavBar(NavGroupName = "Features", NavGroupOrder = 1,
        CascadedLinksGroupName = "Packages", CascadedLinksIcon = "package", CascadedLinksOrder = 1,
        LinkText = "Browse packages", LinkOrder = 1)]
    public async Task<IActionResult> Index(string q = "", int page = 1, string packageType = "any", string framework = "any", bool prerelease = true)
    {
        page = Math.Clamp(page, 1, int.MaxValue / 20);
        var result = await search.SearchAsync(new SearchRequest
        {
            Query = q, Skip = (page - 1) * 20, Take = 20, IncludePrerelease = prerelease,
            IncludeSemVer2 = true, PackageType = packageType == "any" ? null : packageType,
            Framework = string.IsNullOrWhiteSpace(framework) || framework == "any" ? null : framework
        }, HttpContext.RequestAborted);
        return this.StackView(new CatalogViewModel
        {
            Query = q, Page = page, PackageType = packageType, Framework = framework,
            Prerelease = prerelease, TotalHits = result.TotalHits, Packages = result.Data
        });
    }

    [HttpGet("/packages/{id}/{version?}", Name = "PackageDetails")]
    public async Task<IActionResult> Details(string id, string? version = null, string q = "", int page = 1,
        string packageType = "any", string framework = "any", bool prerelease = true, int dependentsPage = 1, string tab = "overview")
    {
        dependentsPage = Math.Clamp(dependentsPage, 1, int.MaxValue / 20);
        var versions = await packages.FindPackagesAsync(id, HttpContext.RequestAborted);
        var selected = NuGetVersion.TryParse(version, out var parsed)
            ? versions.SingleOrDefault(p => p.Version == parsed)
            : versions.Where(p => p.Listed).OrderByDescending(p => p.Version).FirstOrDefault();
        if (selected == null) return NotFound();
        var readme = string.Empty;
        if (selected.HasReadme)
        {
            await using var stream = await content.GetPackageReadmeStreamOrNullAsync(id, selected.Version, HttpContext.RequestAborted);
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                readme = Markdown.ToHtml(await reader.ReadToEndAsync(HttpContext.RequestAborted), ReadmePipeline);
            }
        }
        var types = selected.PackageTypes.Select(t => t.Name).ToList();
        var install = types.Contains("DotnetTool", StringComparer.OrdinalIgnoreCase)
            ? $"dotnet tool install --global {selected.Id} --version {selected.NormalizedVersionString}"
            : types.Contains("Template", StringComparer.OrdinalIgnoreCase)
                ? $"dotnet new install {selected.Id}@{selected.NormalizedVersionString}"
                : $"dotnet add package {selected.Id} --version {selected.NormalizedVersionString}";
        var dependents = await search.FindDependentsAsync(id, (dependentsPage - 1) * 20, 20, HttpContext.RequestAborted);
        return this.StackView(new DetailsViewModel
        {
            PageTitle = selected.Id, Package = selected,
            IconUrl = selected.HasEmbeddedIcon ? urls.GetPackageIconDownloadUrl(selected.Id, selected.Version) : HttpUrl(selected.IconUrl),
            ProjectUrl = HttpUrl(selected.ProjectUrl), SourceUrl = HttpUrl(selected.RepositoryUrl), LicenseUrl = HttpUrl(selected.LicenseUrl),
            IsTemplate = !types.Contains("DotnetTool", StringComparer.OrdinalIgnoreCase) && types.Contains("Template", StringComparer.OrdinalIgnoreCase),
            LastUpdated = versions.Max(p => p.Published), IsLibrary = !types.Any(t =>
                t.Equals("DotnetTool", StringComparison.OrdinalIgnoreCase) || t.Equals("Template", StringComparison.OrdinalIgnoreCase)),
            TotalDependents = dependents.TotalHits, DependentsPage = dependentsPage,
            ActiveTab = tab is "versions" or "dependencies" or "usedby" ? tab : "overview",
            Versions = versions.Where(p => p.Listed).OrderByDescending(p => p.Version).ToList(), Dependents = dependents.Data,
            OwnerDisplayName = await db.PackageFamilies.AsNoTracking()
                .Where(r => r.NormalizedId == id.ToLowerInvariant())
                .Select(r => r.OwnerUser == null ? null : r.OwnerUser.DisplayName)
                .FirstOrDefaultAsync(HttpContext.RequestAborted),
            ReadmeHtml = readme, DownloadUrl = urls.GetPackageDownloadUrl(id, selected.Version), InstallCommand = install,
            Query = q, Page = Math.Max(1, page), PackageType = packageType, Framework = framework, Prerelease = prerelease
        });
    }

    private static string? HttpUrl(Uri? value) => value is { IsAbsoluteUri: true }
        && (value.Scheme == Uri.UriSchemeHttp || value.Scheme == Uri.UriSchemeHttps) ? value.AbsoluteUri : null;

    [HttpGet("/upload")]
    [HttpGet("/Catalog/Upload")]
    [RenderInNavBar(NavGroupName = "Features", NavGroupOrder = 1,
        CascadedLinksGroupName = "Packages", CascadedLinksIcon = "package", CascadedLinksOrder = 1,
        LinkText = "Upload packages", LinkOrder = 2)]
    public IActionResult Upload() => this.StackView(new UploadViewModel
    {
        ServiceIndexUrl = urls.GetServiceIndexUrl(), ReadOnly = configuration.GetValue<bool>("IsReadOnlyMode")
    });
}
