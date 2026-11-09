using Microsoft.AspNetCore.Mvc;

namespace Aiursoft.Nupkg.Controllers.Protocol
{
    public class HealthController : Controller
    {
        [HttpGet]
        [Route("health")]
        public IActionResult Get()
        {
            return Ok(new { status = "Healthy" });
        }
    }
}
