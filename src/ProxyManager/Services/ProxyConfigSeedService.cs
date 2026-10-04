using West94.ProxyManager.Core.AggregatesModel.ProxyHostAggregate;
using West94.ProxyManager.Yarp;

namespace West94.ProxyManager.Services;

/// <summary>
/// At startup, seeds an empty database with any host-based routes found in the loaded
/// ReverseProxy configuration (e.g. proxysettings.{env}.json), then performs the initial load of
/// database routes into <see cref="DatabaseProxyConfigProvider"/>. System routes that use
/// path-only matching (apiRoute, ui-route, etc.) are automatically skipped because they
/// carry no Match.Hosts entries.
/// </summary>
public sealed class ProxyConfigSeedService(
    IServiceScopeFactory scopeFactory,
    // IConfiguration is used here to read the YARP-structured ReverseProxy section, which
    // has no typed-options equivalent. This is an accepted exception to the IOptions<T> rule.
    IConfiguration configuration,
    IProxyConfigReloader reloader,
    ILogger<ProxyConfigSeedService> logger) : IHostedService
{
    /// <summary>
    /// Seeds the database when it is empty, then loads every database route into YARP. The load
    /// runs on every start so existing hosts are routed before Kestrel accepts its first request.
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await SeedIfEmptyAsync(cancellationToken);
        reloader.Reload();
        logger.LogInformation("Loaded database proxy routes at startup.");
    }

    /// <summary>
    /// Adds the host-based routes from configuration to the database when it holds no proxy hosts.
    /// </summary>
    private async Task SeedIfEmptyAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IProxyHostRepository>();

        var existing = await repo.GetAllAsync(cancellationToken);
        if (existing.Count > 0)
        {
            logger.LogDebug("Database already contains {Count} proxy host(s) — skipping seed.", existing.Count);
            return;
        }

        var routeChildren = configuration.GetSection("ReverseProxy:Routes").GetChildren().ToList();
        if (routeChildren.Count == 0)
        {
            logger.LogDebug("No ReverseProxy routes in configuration — skipping seed.");
            return;
        }

        var seeded = 0;
        foreach (var routeSection in routeChildren)
        {
            var hosts = routeSection.GetSection("Match:Hosts").Get<string[]>();

            // System routes (apiRoute, ui-route, etc.) use path-only matching; skip them.
            if (hosts is null || hosts.Length == 0) continue;

            var clusterId = routeSection["ClusterId"];
            if (clusterId is null) continue;

            var address =
                configuration[$"ReverseProxy:Clusters:{clusterId}:Destinations:primary:Address"]
                ?? configuration.GetSection($"ReverseProxy:Clusters:{clusterId}:Destinations")
                                .GetChildren().FirstOrDefault()?["Address"];

            if (address is null)
            {
                logger.LogWarning(
                    "No destination address for cluster '{ClusterId}' — skipping route '{RouteId}'.",
                    clusterId, routeSection.Key);
                continue;
            }

            DestinationUri destination;
            try
            {
                destination = DestinationUri.Parse(address);
            }
            catch (ArgumentException ex)
            {
                logger.LogWarning(ex,
                    "Invalid destination address '{Address}' for route '{RouteId}' — skipping.",
                    address, routeSection.Key);
                continue;
            }

            var proxyHost = ProxyHost.Create(hosts, destination);
            await repo.AddAsync(proxyHost, cancellationToken);
            seeded++;
        }

        if (seeded > 0)
        {
            logger.LogInformation("Seeded {Count} proxy host(s) from configuration.", seeded);
        }
        else
        {
            logger.LogDebug("No host-based routes found in configuration — nothing to seed.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
