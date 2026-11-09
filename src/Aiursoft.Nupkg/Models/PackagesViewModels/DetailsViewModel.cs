using Aiursoft.Nupkg.Core.Search;
using Aiursoft.Nupkg.Entities;
using Aiursoft.UiStack.Layout;
namespace Aiursoft.Nupkg.Models.PackagesViewModels;
public class DetailsViewModel : UiStackLayoutViewModel
{
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
    public string InstallCommand { get; set; } = string.Empty;
}
