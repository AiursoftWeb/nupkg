using Aiursoft.Nupkg.Core.ServiceIndex;
using Aiursoft.Nupkg.Protocol.Models;
using Microsoft.AspNetCore.Mvc;

namespace Aiursoft.Nupkg.Controllers.Protocol
{
    /// <summary>
    /// The NuGet Service Index. This aids NuGet client to discover this server's services.
    /// </summary>
    public class ServiceIndexController(IServiceIndexService serviceIndex) : Controller
    {
        private readonly IServiceIndexService _serviceIndex = serviceIndex ?? throw new ArgumentNullException(nameof(serviceIndex));

        // GET v3/index
        [HttpGet]
        [Route("v3/index.json", Name = Routes.IndexRouteName)]
        public async Task<ServiceIndexResponse> GetAsync(CancellationToken cancellationToken)
        {
            return await _serviceIndex.GetAsync(cancellationToken);
        }
    }
}
