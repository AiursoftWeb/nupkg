using Aiursoft.Nupkg.Core;
using NuGet.Versioning;

namespace Aiursoft.Nupkg
{
    public class NupkgUrlGenerator(IHttpContextAccessor httpContextAccessor, LinkGenerator linkGenerator) : IUrlGenerator
    {
        private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        private readonly LinkGenerator _linkGenerator = linkGenerator ?? throw new ArgumentNullException(nameof(linkGenerator));

        public string GetServiceIndexUrl()
        {
            return _linkGenerator.GetUriByRouteValues(
                _httpContextAccessor.HttpContext ?? throw new ArgumentNullException(nameof(_httpContextAccessor.HttpContext)),
                Routes.IndexRouteName,
                values: null) ?? throw new InvalidOperationException("The NuGet route could not be generated.");
        }

        public string GetPackageContentResourceUrl()
        {
            return AbsoluteUrl("v3/package");
        }

        public string GetPackageMetadataResourceUrl()
        {
            return AbsoluteUrl("v3/registration");
        }

        public string GetPackagePublishResourceUrl()
        {
            return _linkGenerator.GetUriByRouteValues(
                _httpContextAccessor.HttpContext ?? throw new ArgumentNullException(nameof(_httpContextAccessor.HttpContext)),
                Routes.UploadPackageRouteName,
                values: null) ?? throw new InvalidOperationException("The NuGet route could not be generated.");
        }

        public string GetSymbolPublishResourceUrl()
        {
            return _linkGenerator.GetUriByRouteValues(
                _httpContextAccessor.HttpContext ?? throw new ArgumentNullException(nameof(_httpContextAccessor.HttpContext)),
                Routes.UploadSymbolRouteName,
                values: null) ?? throw new InvalidOperationException("The NuGet route could not be generated.");
        }

        public string GetSearchResourceUrl()
        {
            return _linkGenerator.GetUriByRouteValues(
                _httpContextAccessor.HttpContext ?? throw new ArgumentNullException(nameof(_httpContextAccessor.HttpContext)),
                Routes.SearchRouteName,
                values: null) ?? throw new InvalidOperationException("The NuGet route could not be generated.");
        }

        public string GetAutocompleteResourceUrl()
        {
            return _linkGenerator.GetUriByRouteValues(
                _httpContextAccessor.HttpContext ?? throw new ArgumentNullException(nameof(_httpContextAccessor.HttpContext)),
                Routes.AutocompleteRouteName,
                values: null) ?? throw new InvalidOperationException("The NuGet route could not be generated.");
        }

        public string GetRegistrationIndexUrl(string id)
        {
            return _linkGenerator.GetUriByRouteValues(
                _httpContextAccessor.HttpContext ?? throw new ArgumentNullException(nameof(_httpContextAccessor.HttpContext)),
                Routes.RegistrationIndexRouteName,
                values: new { Id = id.ToLowerInvariant() }) ?? throw new InvalidOperationException("The NuGet route could not be generated.");
        }

        public string GetRegistrationPageUrl(string id, NuGetVersion lower, NuGetVersion upper)
        {
            // Nupkg does not support paging the registration resource.
            throw new NotImplementedException();
        }

        public string GetRegistrationLeafUrl(string id, NuGetVersion version)
        {
            return _linkGenerator.GetUriByRouteValues(
                _httpContextAccessor.HttpContext ?? throw new ArgumentNullException(nameof(_httpContextAccessor.HttpContext)),
                Routes.RegistrationLeafRouteName,
                values: new
                {
                    Id = id.ToLowerInvariant(),
                    Version = version.ToNormalizedString().ToLowerInvariant(),
                }) ?? throw new InvalidOperationException("The NuGet route could not be generated.");
        }

        public string GetPackageVersionsUrl(string id)
        {
            return _linkGenerator.GetUriByRouteValues(
                _httpContextAccessor.HttpContext ?? throw new ArgumentNullException(nameof(_httpContextAccessor.HttpContext)),
                Routes.PackageVersionsRouteName,
                values: new { Id = id.ToLowerInvariant() }) ?? throw new InvalidOperationException("The NuGet route could not be generated.");
        }

        public string GetPackageDownloadUrl(string id, NuGetVersion version)
        {
            id = id.ToLowerInvariant();
            var versionString = version.ToNormalizedString().ToLowerInvariant();

            return _linkGenerator.GetUriByRouteValues(
                _httpContextAccessor.HttpContext ?? throw new ArgumentNullException(nameof(_httpContextAccessor.HttpContext)),
                Routes.PackageDownloadRouteName,
                values: new
                {
                    Id = id,
                    Version = versionString,
                    IdVersion = $"{id}.{versionString}"
                }) ?? throw new InvalidOperationException("The NuGet route could not be generated.");
        }

        public string GetPackageManifestDownloadUrl(string id, NuGetVersion version)
        {
            id = id.ToLowerInvariant();
            var versionString = version.ToNormalizedString().ToLowerInvariant();

            return _linkGenerator.GetUriByRouteValues(
                _httpContextAccessor.HttpContext ?? throw new ArgumentNullException(nameof(_httpContextAccessor.HttpContext)),
                Routes.PackageDownloadRouteName,
                values: new
                {
                    Id = id,
                    Version = versionString,
                    Id2 = id,
                }) ?? throw new InvalidOperationException("The NuGet route could not be generated.");
        }

        public string GetPackageIconDownloadUrl(string id, NuGetVersion version)
        {
            id = id.ToLowerInvariant();
            var versionString = version.ToNormalizedString().ToLowerInvariant();

            return _linkGenerator.GetUriByRouteValues(
                _httpContextAccessor.HttpContext ?? throw new ArgumentNullException(nameof(_httpContextAccessor.HttpContext)),
                Routes.PackageDownloadIconRouteName,
                values: new
                {
                    Id = id,
                    Version = versionString
                }) ?? throw new InvalidOperationException("The NuGet route could not be generated.");
        }

        private string AbsoluteUrl(string relativePath)
        {
            var request = _httpContextAccessor.HttpContext?.Request ?? throw new ArgumentNullException(nameof(_httpContextAccessor.HttpContext));

            return string.Concat(
                request.Scheme,
                "://",
                request.Host.ToUriComponent(),
                request.PathBase.ToUriComponent(),
                "/",
                relativePath);
        }
    }
}
