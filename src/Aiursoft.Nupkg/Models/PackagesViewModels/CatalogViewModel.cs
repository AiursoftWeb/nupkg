using Aiursoft.Nupkg.Protocol.Models;
using Aiursoft.UiStack.Layout;
namespace Aiursoft.Nupkg.Models.PackagesViewModels;
public class CatalogViewModel : UiStackLayoutViewModel
{
    public CatalogViewModel() { PageTitle = "Packages"; }
    public string Query { get; set; } = string.Empty;
    public string PackageType { get; set; } = "any";
    public string Framework { get; set; } = "any";
    public bool Prerelease { get; set; } = true;
    public int Page { get; set; } = 1;
    public long TotalHits { get; set; }
    public long TotalPages => (TotalHits + 19) / 20;
    public IReadOnlyList<SearchResult> Packages { get; set; } = [];
}
