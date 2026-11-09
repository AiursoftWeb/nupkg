using Microsoft.AspNetCore.Mvc;
namespace Aiursoft.Nupkg.Controllers;
public class DashboardController : Controller
{
    public IActionResult Index() => RedirectToRoute("PackageCatalog");
}
