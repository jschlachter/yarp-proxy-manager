using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace West94.ProxyManager.Files.Data;

/// <summary>Used by EF Core design-time tools (dotnet ef migrations) when no startup project connection string is available.</summary>
public sealed class FilesDbContextFactory : IDesignTimeDbContextFactory<FilesDbContext>
{
    /// <summary>Environment variable holding the design-time PostgreSQL connection string.</summary>
    private const string ConnectionStringEnvVar = "PROXYMANAGER_FILES_DB_CONNECTION";

    private const string DefaultConnectionString =
        "Host=localhost;Port=5432;Database=proxymanager;Username=proxymanager;Password=proxymanager";

    public FilesDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable(ConnectionStringEnvVar) is { Length: > 0 } fromEnv
                ? fromEnv
                : DefaultConnectionString;

        var options = new DbContextOptionsBuilder<FilesDbContext>()
            .UseNpgsql(
                connectionString,
                o =>
                {
                    o.MigrationsAssembly(typeof(FilesDbContext).Assembly.FullName);
                    o.MigrationsHistoryTable("__ef_migrations_history", "files");
                })
            .Options;

        return new FilesDbContext(options);
    }
}
