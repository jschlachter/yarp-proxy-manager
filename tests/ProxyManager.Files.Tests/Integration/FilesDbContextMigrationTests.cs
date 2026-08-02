using Microsoft.EntityFrameworkCore;
using West94.ProxyManager.Files.Data;

namespace West94.ProxyManager.Files.Tests.Integration;

/// <summary>
/// Phase 2 acceptance gate: proves the "files" schema and its own migrations-history table
/// exist and are isolated from ProxyManagerDbContext's default-schema history.
/// Requires PROXYMANAGER_FILES_DB_CONNECTION pointed at a Postgres instance where the
/// InitialCreate migration has already been applied via `dotnet ef database update`.
/// </summary>
[Trait("Category", "Integration")]
public sealed class FilesDbContextMigrationTests
{
    [Fact]
    public async Task InitialCreateMigration_IsAppliedAndIsolatedInFilesSchema()
    {
        var connectionString = Environment.GetEnvironmentVariable("PROXYMANAGER_FILES_DB_CONNECTION")
            ?? throw new InvalidOperationException("PROXYMANAGER_FILES_DB_CONNECTION must be set to run this integration test.");

        var options = new DbContextOptionsBuilder<FilesDbContext>()
            .UseNpgsql(connectionString, o =>
            {
                o.MigrationsAssembly(typeof(FilesDbContext).Assembly.FullName);
                o.MigrationsHistoryTable("__ef_migrations_history", "files");
            })
            .Options;

        await using var db = new FilesDbContext(options);
        var ct = TestContext.Current.CancellationToken;

        var applied = await db.Database.GetAppliedMigrationsAsync(ct);

        Assert.Contains(applied, m => m.Contains("InitialCreate", StringComparison.Ordinal));
    }
}
