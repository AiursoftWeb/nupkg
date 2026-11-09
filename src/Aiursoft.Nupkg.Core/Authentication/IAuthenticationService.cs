namespace Aiursoft.Nupkg.Core.Authentication
{
    public interface IAuthenticationService
    {
        Task<bool> AuthenticateAsync(string apiKey, CancellationToken cancellationToken);
    }
}
