using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace West94.ProxyManager.Infrastructure.Data;

/// <summary>Used by EF Core design-time tools (dotnet ef migrations) when no startup project connection string is available.</summary>
public sealed class ProxyManagerDbContextFactory : IDesignTimeDbContextFactory<ProxyManagerDbContext>
{
    /// <summary>Environment variable holding the design-time PostgreSQL connection string.</summary>
    private const string ConnectionStringEnvVar = "PROXYMANAGER_DB_CONNECTION";

    private const string DefaultConnectionString =
        "Host=localhost;Port=5432;Database=proxymanager;Username=proxymanager;Password=proxymanager";

    public ProxyManagerDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable(ConnectionStringEnvVar) is { Length: > 0 } fromEnv
                ? fromEnv
                : DefaultConnectionString;

        var options = new DbContextOptionsBuilder<ProxyManagerDbContext>()
            .UseNpgsql(
                connectionString,
                o => o.MigrationsAssembly(typeof(ProxyManagerDbContext).Assembly.FullName))
            .Options;

        return new ProxyManagerDbContext(options);
    }
}
