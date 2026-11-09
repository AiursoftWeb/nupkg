using Aiursoft.Nupkg.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.Nupkg.InMemory;

public class InMemoryContext(DbContextOptions<InMemoryContext> options) : NupkgDbContext(options)
{
    public override Task MigrateAsync(CancellationToken cancellationToken)
    {
        return Database.EnsureCreatedAsync(cancellationToken);
    }

    public override Task<bool> CanConnectAsync()
    {
        return Task.FromResult(true);
    }
}
