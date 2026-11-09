using System.Diagnostics.CodeAnalysis;
using Aiursoft.DbTools;
using Aiursoft.DbTools.MySql;
using Aiursoft.Nupkg.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Aiursoft.Nupkg.MySql;

[ExcludeFromCodeCoverage]
public class MySqlSupportedDb(bool allowCache, bool splitQuery) : SupportedDatabaseType<NupkgDbContext>
{
    public override string DbType => "MySql";

    public override IServiceCollection RegisterFunction(IServiceCollection services, string connectionString)
    {
        return services.AddAiurMySqlWithCache<MySqlContext>(
            connectionString,
            splitQuery: splitQuery,
            allowCache: allowCache);
    }

    public override NupkgDbContext ContextResolver(IServiceProvider serviceProvider)
    {
        return serviceProvider.GetRequiredService<MySqlContext>();
    }
}
