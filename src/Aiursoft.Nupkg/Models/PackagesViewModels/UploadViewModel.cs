using Aiursoft.UiStack.Layout;
namespace Aiursoft.Nupkg.Models.PackagesViewModels;
public class UploadViewModel : UiStackLayoutViewModel
{
    public UploadViewModel() { PageTitle = "Upload packages"; }
    public string ServiceIndexUrl { get; set; } = string.Empty;
    public bool ReadOnly { get; set; }
}
