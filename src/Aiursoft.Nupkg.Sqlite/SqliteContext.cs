using System.Diagnostics.CodeAnalysis;
using Aiursoft.Nupkg.Entities;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.Nupkg.Sqlite;

[ExcludeFromCodeCoverage]

public class SqliteContext(DbContextOptions<SqliteContext> options) : NupkgDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<Package>().Property(p => p.Id).HasColumnType("TEXT COLLATE NOCASE");
        builder.Entity<Package>().Property(p => p.NormalizedVersionString).HasColumnType("TEXT COLLATE NOCASE");
        builder.Entity<PackageDependency>().Property(p => p.Id).HasColumnType("TEXT COLLATE NOCASE");
        builder.Entity<PackageType>().Property(p => p.Name).HasColumnType("TEXT COLLATE NOCASE");
        builder.Entity<TargetFramework>().Property(p => p.Moniker).HasColumnType("TEXT COLLATE NOCASE");
    }

    public override Task<bool> CanConnectAsync()
    {
        return Task.FromResult(true);
    }
}
