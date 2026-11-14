using Aiursoft.Nupkg.Entities;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Aiursoft.Nupkg.MySql;
using Aiursoft.Nupkg.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Aiursoft.Nupkg.Tests.IntegrationTests;

/// <summary>
/// This test class ensures that the Entity Framework migrations are up-to-date for all supported database providers.
/// If you change the database model (entities), you must create a new migration for both SQLite and MySQL.
/// </summary>
[TestClass]
public class MigrationTests
{
    [TestMethod]
    public async Task LocalAuthorityMigrationPreservesVersionsAndDistinguishesMirrors()
    {
        await using var context = new SqliteContext(new DbContextOptionsBuilder<SqliteContext>()
            .UseSqlite("DataSource=:memory:").Options);
        await context.Database.OpenConnectionAsync();
        var migrator = context.GetService<IMigrator>();
        var previous = context.Database.GetMigrations().Reverse().Skip(1).First();
        await migrator.MigrateAsync(previous);
        for (var id = 1; id <= 2; id++)
        {
            var name = id == 1 ? "local.history" : "mirror.history";
            await context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO PackageFamilies (Id, PackageId, NormalizedId, CreatedAt) VALUES ({id}, {name}, {name}, {DateTime.UtcNow})");
            context.Packages.Add(new Package
            {
                PackageFamilyId = id, Id = name, NormalizedVersionString = "1.0.0",
                Authors = [], Tags = [], Description = "Historical package", Published = DateTime.UtcNow,
                IsLocallyPublished = id == 1, MirrorSource = id == 2 ? "https://api.nuget.org/v3/index.json" : null
            });
        }
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        await migrator.MigrateAsync();
        Assert.IsTrue((await context.PackageFamilies.SingleAsync(f => f.Id == 1)).IsLocallyPublished);
        Assert.IsFalse((await context.PackageFamilies.SingleAsync(f => f.Id == 2)).IsLocallyPublished);
        Assert.AreEqual(2, await context.Packages.IgnoreQueryFilters().CountAsync());
        Assert.AreEqual(1, await context.Packages.CountAsync(p => p.Id == "local.history"));
        await migrator.MigrateAsync(previous);
        context.ChangeTracker.Clear();
        await migrator.MigrateAsync();
        Assert.IsTrue((await context.PackageFamilies.SingleAsync(f => f.Id == 1)).IsLocallyPublished);
        Assert.IsFalse((await context.PackageFamilies.SingleAsync(f => f.Id == 2)).IsLocallyPublished);
        Assert.AreEqual(2, await context.Packages.IgnoreQueryFilters().CountAsync());
    }

    [TestMethod]
    public void TestSqliteMigrations()
    {
        var options = new DbContextOptionsBuilder<SqliteContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        using var context = new SqliteContext(options);
        var hasPendingChanges = context.Database.HasPendingModelChanges();
        Assert.IsFalse(hasPendingChanges, "There are pending model changes for Sqlite. Please run 'dotnet ef migrations add' for Sqlite.");
    }

    [TestMethod]
    public void TestMySqlMigrations()
    {
        var options = new DbContextOptionsBuilder<MySqlContext>()
            .UseMySql("Server=localhost;Database=test;Uid=root;Pwd=password;", new MySqlServerVersion(new Version(8, 0, 31)))
            .Options;
        using var context = new MySqlContext(options);
        var hasPendingChanges = context.Database.HasPendingModelChanges();
        Assert.IsFalse(hasPendingChanges, "There are pending model changes for MySql. Please run 'dotnet ef migrations add' for MySql.");
    }
}
