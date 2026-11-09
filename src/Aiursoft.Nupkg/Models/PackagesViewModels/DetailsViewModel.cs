using Aiursoft.Nupkg.Core.Search;
using Aiursoft.Nupkg.Entities;
using Aiursoft.UiStack.Layout;
namespace Aiursoft.Nupkg.Models.PackagesViewModels;
public class DetailsViewModel : UiStackLayoutViewModel
{
    public string? OwnerDisplayName { get; set; }
    public Package Package { get; set; } = null!;
    public IReadOnlyList<Package> Versions { get; set; } = [];
    public IReadOnlyList<PackageDependent> Dependents { get; set; } = [];
    public string ReadmeHtml { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public string PackageType { get; set; } = "any";
    public string Framework { get; set; } = "any";
    public bool Prerelease { get; set; } = true;
    public int Page { get; set; } = 1;
    public long TotalDependents { get; set; }
    public int DependentsPage { get; set; } = 1;
    public string ActiveTab { get; set; } = "overview";
    public string? IconUrl { get; set; }
    public string? ProjectUrl { get; set; }
    public string? SourceUrl { get; set; }
    public string? LicenseUrl { get; set; }
    public DateTime LastUpdated { get; set; }
    public bool IsLibrary { get; set; }
    public string PackageReferenceCommand => $"<PackageReference Include=\"{Package.Id}\" Version=\"{Package.NormalizedVersionString}\" />";
    public string PackageManagerCommand => $"Install-Package {Package.Id} -Version {Package.NormalizedVersionString}";
    public string PaketCommand => $"paket add {Package.Id} --version {Package.NormalizedVersionString}";
    public string InstallCommand { get; set; } = string.Empty;
}
