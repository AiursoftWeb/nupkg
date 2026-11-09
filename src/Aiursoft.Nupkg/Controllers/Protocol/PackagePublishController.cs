using Aiursoft.Nupkg.Core;
using Aiursoft.Nupkg.Services.Authentication;
using Aiursoft.Nupkg.Core.Configuration;
using Aiursoft.Nupkg.Core.Indexing;
using Aiursoft.Nupkg.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NuGet.Versioning;

namespace Aiursoft.Nupkg.Controllers.Protocol
{
    public class PackagePublishController(
        PackagePublisherAuthenticationService authentication,
        IPackageIndexingService indexer,
        PackageDatabase packages,
        IPackageDeletionService deletionService,
        IOptionsSnapshot<NupkgOptions> options,
        ILogger<PackagePublishController> logger)
        : Controller
    {
        private readonly PackagePublisherAuthenticationService _authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
        private readonly IPackageIndexingService _indexer = indexer ?? throw new ArgumentNullException(nameof(indexer));
        private readonly PackageDatabase _packages = packages ?? throw new ArgumentNullException(nameof(packages));
        private readonly IPackageDeletionService _deleteService = deletionService ?? throw new ArgumentNullException(nameof(deletionService));
        private readonly IOptionsSnapshot<NupkgOptions> _options = options ?? throw new ArgumentNullException(nameof(options));
        private readonly ILogger<PackagePublishController> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // See: https://docs.microsoft.com/en-us/nuget/api/package-publish-resource#push-a-package
        [Route("api/v2/package", Name = Routes.UploadPackageRouteName)]
        [HttpPut]
        public async Task<IActionResult> Upload(CancellationToken cancellationToken)
        {
            if (_options.Value.IsReadOnlyMode) return Unauthorized();
            var publisher = await _authentication.AuthenticateAsync(Request.GetApiKey(), cancellationToken);
            if (publisher == null) return Unauthorized();
            if (!publisher.CanPublish && !publisher.CanManagePackages) return StatusCode(403);

            try
            {
                await using (var uploadStream = await Request.GetUploadStreamOrNullAsync(cancellationToken))
                {
                    if (uploadStream == null)
                    {
                        return BadRequest();
                    }

                    var result = await _indexer.IndexAsync(uploadStream, publisher, cancellationToken);

                    return result switch
                    {
                        PackageIndexingResult.InvalidPackage => BadRequest(),
                        PackageIndexingResult.PackageAlreadyExists => Conflict(),
                        PackageIndexingResult.Success => StatusCode(201),
                        PackageIndexingResult.Forbidden => StatusCode(403),
                        _ => StatusCode(500)
                    };
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exception thrown during package upload");

                return StatusCode(500);
            }
        }

        [HttpDelete]
        [Route("api/v2/package/{id}/{version}", Name = Routes.DeleteRouteName)]
        public async Task<IActionResult> Delete(string id, string version, CancellationToken cancellationToken)
        {
            if (_options.Value.IsReadOnlyMode)
            {
                return Unauthorized();
            }

            if (!NuGetVersion.TryParse(version, out var nugetVersion))
            {
                return NotFound();
            }

            var publisher = await _authentication.AuthenticateAsync(Request.GetApiKey(), cancellationToken);
            if (publisher == null) return Unauthorized();
            if (!await _packages.CanManagePackageAsync(id, publisher, cancellationToken)) return StatusCode(403);

            if (await _deleteService.TryDeletePackageAsync(id, nugetVersion, cancellationToken))
            {
                return NoContent();
            }
            else
            {
                return NotFound();
            }
        }

        [HttpPost]
        [Route("api/v2/package/{id}/{version}", Name = Routes.RelistRouteName)]
        public async Task<IActionResult> Relist(string id, string version, CancellationToken cancellationToken)
        {
            if (_options.Value.IsReadOnlyMode)
            {
                return Unauthorized();
            }

            if (!NuGetVersion.TryParse(version, out var nugetVersion))
            {
                return NotFound();
            }

            var publisher = await _authentication.AuthenticateAsync(Request.GetApiKey(), cancellationToken);
            if (publisher == null) return Unauthorized();
            if (!await _packages.CanManagePackageAsync(id, publisher, cancellationToken)) return StatusCode(403);

            if (await _packages.RelistPackageAsync(id, nugetVersion, cancellationToken))
            {
                return Ok();
            }
            else
            {
                return NotFound();
            }
        }
    }
}
