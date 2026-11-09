using Aiursoft.Nupkg.Core.Content;
using Aiursoft.Nupkg.Protocol.Models;
using Microsoft.AspNetCore.Mvc;
using NuGet.Versioning;

namespace Aiursoft.Nupkg.Controllers.Protocol
{
    /// <summary>
    /// The Package Content resource, used to download content from packages.
    /// See: https://docs.microsoft.com/en-us/nuget/api/package-base-address-resource
    /// </summary>
    public class PackageContentController(IPackageContentService content) : Controller
    {
        private readonly IPackageContentService _content = content ?? throw new ArgumentNullException(nameof(content));

        [Route("v3/package/{id}/index.json", Name = Routes.PackageVersionsRouteName)]
        public async Task<ActionResult<PackageVersionsResponse>> GetPackageVersionsAsync(string id, CancellationToken cancellationToken)
        {
            var versions = await _content.GetPackageVersionsOrNullAsync(id, cancellationToken);
            if (versions == null)
            {
                return NotFound();
            }

            return versions;
        }

        [Route("v3/package/{id}/{version}/{idVersion}.nupkg", Name = Routes.PackageDownloadRouteName)]
        public async Task<IActionResult> DownloadPackageAsync(string id, string version, CancellationToken cancellationToken)
        {
            if (!NuGetVersion.TryParse(version, out var nugetVersion))
            {
                return NotFound();
            }

            var packageStream = await _content.GetPackageContentStreamOrNullAsync(id, nugetVersion, cancellationToken);
            if (packageStream == null)
            {
                return NotFound();
            }

            return File(packageStream, "application/octet-stream");
        }

        [Route("v3/package/{id}/{version}/{id2}.nuspec", Name = Routes.PackageDownloadManifestRouteName)]
        public async Task<IActionResult> DownloadNuspecAsync(string id, string version, CancellationToken cancellationToken)
        {
            if (!NuGetVersion.TryParse(version, out var nugetVersion))
            {
                return NotFound();
            }

            var nuspecStream = await _content.GetPackageManifestStreamOrNullAsync(id, nugetVersion, cancellationToken);
            if (nuspecStream == null)
            {
                return NotFound();
            }

            return File(nuspecStream, "text/xml");
        }

        [Route("v3/package/{id}/{version}/readme", Name = Routes.PackageDownloadReadmeRouteName)]
        public async Task<IActionResult> DownloadReadmeAsync(string id, string version, CancellationToken cancellationToken)
        {
            if (!NuGetVersion.TryParse(version, out var nugetVersion))
            {
                return NotFound();
            }

            var readmeStream = await _content.GetPackageReadmeStreamOrNullAsync(id, nugetVersion, cancellationToken);
            if (readmeStream == null)
            {
                return NotFound();
            }

            return File(readmeStream, "text/markdown");
        }

        [Route("v3/package/{id}/{version}/icon", Name = Routes.PackageDownloadIconRouteName)]
        public async Task<IActionResult> DownloadIconAsync(string id, string version, CancellationToken cancellationToken)
        {
            if (!NuGetVersion.TryParse(version, out var nugetVersion))
            {
                return NotFound();
            }

            var iconStream = await _content.GetPackageIconStreamOrNullAsync(id, nugetVersion, cancellationToken);
            if (iconStream == null)
            {
                return NotFound();
            }

            return File(iconStream, "image/xyz");
        }
    }
}
