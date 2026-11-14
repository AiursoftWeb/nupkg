using Aiursoft.Nupkg.Models.HomeViewModels;
using Aiursoft.Nupkg.Services;
using Aiursoft.UiStack.Layout;
using Aiursoft.WebTools.Attributes;
using Microsoft.AspNetCore.Mvc;

namespace Aiursoft.Nupkg.Controllers;

[LimitPerMin]
public class HomeController : Controller
{
    public IActionResult Index()
    {
        var model = new IndexViewModel();
        var view = this.SimpleView(model);
        model.Theme = Request.Cookies[ThemeController.ThemeCookieKey] == bool.TrueString ? UiTheme.Dark : UiTheme.Light;
        return view;
    }
}
