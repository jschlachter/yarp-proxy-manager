using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace West94.ProxyManager.Infrastructure.Data;

/// <summary>Used by EF Core design-time tools (dotnet ef migrations) when no startup project connection string is available.</summary>
public sealed class ProxyManagerDbContextFactory : IDesignTimeDbContextFactory<ProxyManagerDbContext>
{
    public ProxyManagerDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ProxyManagerDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5432;Database=proxymanager;Username=proxymanager;Password=proxymanager",
                o => o.MigrationsAssembly(typeof(ProxyManagerDbContext).Assembly.FullName))
            .Options;

        return new ProxyManagerDbContext(options);
    }
}
