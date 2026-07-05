using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using West94.ProxyManager.Core.AggregatesModel.AuditLogAggregate;
using West94.ProxyManager.Core.AggregatesModel.CertificateAggregate;
using West94.ProxyManager.Core.AggregatesModel.ProxyHostAggregate;
using West94.ProxyManager.Infrastructure.Data;
using West94.ProxyManager.Infrastructure.Options;
using West94.ProxyManager.Infrastructure.Repositories;

namespace West94.ProxyManager.Infrastructure.Extensions;

public static class InfrastructureServiceExtensions
{
    private static readonly Regex TokenPattern = new(@"\{\{([A-Za-z0-9_]+)\}\}", RegexOptions.Compiled);

    /// <summary>Registers EF Core DbContext and repository implementations backed by PostgreSQL.</summary>
    public static IServiceCollection AddProxyManagerInfrastructure(this IServiceCollection services)
    {
        // Connection string resolved lazily from IOptions<DatabaseOptions> so that WebApplicationFactory
        // can inject test configuration via ConfigureWebHost before the DbContext is first created.
        services.AddDbContext<ProxyManagerDbContext>((sp, options) =>
        {
            var rawCs = sp.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString;
            var configuration = sp.GetRequiredService<IConfiguration>();
            var logger = sp.GetRequiredService<ILoggerFactory>()
                           .CreateLogger(nameof(InfrastructureServiceExtensions));

            var cs = InterpolateTokens(rawCs, configuration, logger);
            options.UseNpgsql(cs, o => o.MigrationsAssembly(typeof(ProxyManagerDbContext).Assembly.FullName));
        });

        services.AddScoped<IProxyHostRepository, PostgresProxyHostRepository>();
        services.AddScoped<IAuditLogRepository, PostgresAuditLogRepository>();
        services.AddScoped<ICertificateRepository, PostgresCertificateRepository>();

        return services;
    }

    private static string InterpolateTokens(string connectionString, IConfiguration configuration, ILogger logger)
    {
        var missing = new List<string>();

        var result = TokenPattern.Replace(connectionString, match =>
        {
            var key = match.Groups[1].Value;
            var value = configuration[key];
            if (value is null)
            {
                missing.Add(key);
                return match.Value;
            }
            return value;
        });

        if (missing.Count > 0)
        {
            var keys = string.Join(", ", missing);
            logger.LogError("Connection string contains unresolved tokens; missing configuration keys: {Keys}", keys);
            
            
        }

        return result;
    }
}
