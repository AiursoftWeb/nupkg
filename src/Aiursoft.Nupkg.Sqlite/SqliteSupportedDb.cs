using System.Diagnostics.CodeAnalysis;
using Aiursoft.DbTools;
using Aiursoft.DbTools.Sqlite;
using Aiursoft.Nupkg.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Aiursoft.Nupkg.Sqlite;

[ExcludeFromCodeCoverage]
public class SqliteSupportedDb(bool allowCache, bool splitQuery) : SupportedDatabaseType<NupkgDbContext>
{
    public override string DbType => "Sqlite";

    public override IServiceCollection RegisterFunction(IServiceCollection services, string connectionString)
    {
        return services.AddAiurSqliteWithCache<SqliteContext>(
            connectionString,
            splitQuery: splitQuery,
            allowCache: allowCache);
    }

    public override NupkgDbContext ContextResolver(IServiceProvider serviceProvider)
    {
        return serviceProvider.GetRequiredService<SqliteContext>();
    }
}
