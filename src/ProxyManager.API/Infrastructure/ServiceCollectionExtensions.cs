using West94.ProxyManager.Core.AggregatesModel.AuditLogAggregate;
using West94.ProxyManager.Core.AggregatesModel.ProxyHostAggregate;
using West94.ProxyManager.Infrastructure.Repositories;

namespace West94.ProxyManager.API.Infrastructure;

public static class ServiceCollectionExtensions
{
    /// <summary>Registers in-memory repository implementations for proxy hosts and audit log.</summary>
    public static IServiceCollection AddProxyManagerServices(this IServiceCollection services)
    {
        services.AddSingleton<IProxyHostRepository, InMemoryProxyHostRepository>();
        services.AddSingleton<IAuditLogRepository, InMemoryAuditLogRepository>();
        return services;
    }
}
