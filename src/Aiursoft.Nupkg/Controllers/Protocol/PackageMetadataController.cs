using Aiursoft.Nupkg.Core.Metadata;
using Aiursoft.Nupkg.Core;
using Aiursoft.Nupkg.Core.Configuration;
using Microsoft.Extensions.Options;
using Aiursoft.Nupkg.Protocol.Models;
using Microsoft.AspNetCore.Mvc;
using NuGet.Versioning;

namespace Aiursoft.Nupkg.Controllers.Protocol
{
    /// <summary>
    /// The Package Metadata resource, used to fetch packages' information.
    /// See: https://docs.microsoft.com/en-us/nuget/api/registration-base-url-resource
    /// </summary>
    public class PackageMetadataController(IPackageMetadataService metadata, IPackageService packages,
        RegistrationBuilder builder, IOptions<S360MirrorOptions> options) : Controller
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

            if (options.Value.Enabled)
            {
                // Registration is metadata discovery, not an archive download request.
                var package = (await packages.FindPackagesAsync(id, cancellationToken))
                    .SingleOrDefault(p => p.Version == nugetVersion);
                return package == null ? NotFound() : builder.BuildLeaf(package);
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
