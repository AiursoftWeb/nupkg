using Aiursoft.Nupkg.Core.Metadata;
using Aiursoft.Nupkg.Protocol.Models;
using Microsoft.AspNetCore.Mvc;
using NuGet.Versioning;

namespace Aiursoft.Nupkg.Controllers.Protocol
{
    /// <summary>
    /// The Package Metadata resource, used to fetch packages' information.
    /// See: https://docs.microsoft.com/en-us/nuget/api/registration-base-url-resource
    /// </summary>
    public class PackageMetadataController(IPackageMetadataService metadata) : Controller
    {
        private readonly IPackageMetadataService _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));

        // GET v3/registration/{id}.json
        [HttpGet]
        [Route("v3/registration/{id}/index.json", Name = Routes.RegistrationIndexRouteName)]
        public async Task<ActionResult<NupkgRegistrationIndexResponse>> RegistrationIndexAsync(string id, CancellationToken cancellationToken)
        {
            var index = await _metadata.GetRegistrationIndexOrNullAsync(id, cancellationToken);
            if (index == null)
            {
                return NotFound();
            }

            return index;
        }

        // GET v3/registration/{id}/{version}.json
        [HttpGet]
        [Route("v3/registration/{id}/{version}.json", Name = Routes.RegistrationLeafRouteName)]
        public async Task<ActionResult<RegistrationLeafResponse>> RegistrationLeafAsync(string id, string version, CancellationToken cancellationToken)
        {
            if (!NuGetVersion.TryParse(version, out var nugetVersion))
            {
                return NotFound();
            }

            var leaf = await _metadata.GetRegistrationLeafOrNullAsync(id, nugetVersion, cancellationToken);
            if (leaf == null)
            {
                return NotFound();
            }

            return leaf;
        }
    }
}
