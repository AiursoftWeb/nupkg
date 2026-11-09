using Aiursoft.Nupkg.Configuration;
using Microsoft.AspNetCore.Mvc;
using Aiursoft.Nupkg.Services;
using Aiursoft.Nupkg.Services.FileStorage;

namespace Aiursoft.Nupkg.Views.Shared.Components.MarketingFooter;

public class MarketingFooter(
    GlobalSettingsService globalSettingsService,
    StorageService storageService) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(MarketingFooterViewModel? model = null)
    {
        model ??= new MarketingFooterViewModel();
        model.BrandName = await globalSettingsService.GetSettingValueAsync(SettingsMap.BrandName);
        model.BrandHomeUrl = await globalSettingsService.GetSettingValueAsync(SettingsMap.BrandHomeUrl);
        model.Icp = await globalSettingsService.GetSettingValueAsync(SettingsMap.Icp);
        
        var logoPath = await globalSettingsService.GetSettingValueAsync(SettingsMap.ProjectLogo);
        if (!string.IsNullOrWhiteSpace(logoPath))
        {
            model.LogoUrl = storageService.RelativePathToInternetUrl(logoPath, HttpContext);
        }
        
        return View(model);
    }
}
