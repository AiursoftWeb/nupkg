using Microsoft.AspNetCore.Mvc;
namespace Aiursoft.Nupkg.Controllers;
public class HomeController : Controller
{
    public IActionResult Index() => RedirectToRoute("PackageCatalog");
}
