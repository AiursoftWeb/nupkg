using Microsoft.Extensions.DependencyInjection;

namespace Aiursoft.Nupkg.Core
{
    public class NupkgApplication(IServiceCollection services)
    {
        public IServiceCollection Services { get; } = services ?? throw new ArgumentNullException(nameof(services));
    }
}
