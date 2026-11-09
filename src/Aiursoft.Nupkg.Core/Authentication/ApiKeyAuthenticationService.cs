using Aiursoft.Nupkg.Core.Configuration;
using Microsoft.Extensions.Options;

namespace Aiursoft.Nupkg.Core.Authentication
{
    public class ApiKeyAuthenticationService : IAuthenticationService
    {
        private readonly string _apiKey;
        private readonly bool _enabled;
        private readonly bool _allowAnonymous;

        public ApiKeyAuthenticationService(IOptionsSnapshot<NupkgOptions> options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            _apiKey = string.IsNullOrEmpty(options.Value.ApiKey) ? null : options.Value.ApiKey;
            _enabled = options.Value.EnableLegacyApiKey;
            _allowAnonymous = options.Value.AllowAnonymousPublishing;
        }

        public Task<bool> AuthenticateAsync(string apiKey, CancellationToken cancellationToken)
            => Task.FromResult(Authenticate(apiKey));

        private bool Authenticate(string apiKey)
        {
            if (!_enabled) return false;
            if (_apiKey == null) return _allowAnonymous;

            return _apiKey == apiKey;
        }
    }
}
