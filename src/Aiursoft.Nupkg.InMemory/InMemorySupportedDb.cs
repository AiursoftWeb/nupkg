using Aiursoft.DbTools;
using Aiursoft.DbTools.InMemory;
using Aiursoft.Nupkg.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Aiursoft.Nupkg.InMemory;

public class InMemorySupportedDb : SupportedDatabaseType<NupkgDbContext>
{
    public override string DbType => "InMemory";

    public override IServiceCollection RegisterFunction(IServiceCollection services, string connectionString)
    {
        return services.AddAiurInMemoryDb<InMemoryContext>();
    }

    public override NupkgDbContext ContextResolver(IServiceProvider serviceProvider)
    {
        return serviceProvider.GetRequiredService<InMemoryContext>();
    }
}
