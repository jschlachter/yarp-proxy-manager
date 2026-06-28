using Microsoft.Extensions.Primitives;
using West94.ProxyManager.Core.AggregatesModel.ProxyHostAggregate;
using Yarp.ReverseProxy.Configuration;

namespace West94.ProxyManager.Yarp;

/// <summary>
/// YARP <see cref="IProxyConfigProvider"/> that loads user-defined proxy routes from the database.
/// Starts with an empty config; call <see cref="Reload"/> to populate routes and to signal YARP
/// to re-read the configuration.
/// </summary>
public sealed class DatabaseProxyConfigProvider(IServiceScopeFactory scopeFactory)
    : IProxyConfigProvider, IProxyConfigReloader
{
    private InMemoryConfig _currentConfig = new([], []);

    public IProxyConfig GetConfig() => Volatile.Read(ref _currentConfig);

    public void Reload()
    {
        using var scope = scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IProxyHostRepository>();
        var hosts = repo.GetAllAsync().GetAwaiter().GetResult();
        var (routes, clusters) = ProxyHostYarpTranslator.Translate(hosts);
        var newConfig = new InMemoryConfig(routes, clusters);
        var old = Interlocked.Exchange(ref _currentConfig, newConfig);
        old.SignalChange();
    }
}

internal sealed class InMemoryConfig : IProxyConfig
{
    private readonly CancellationTokenSource _cts = new();

    internal InMemoryConfig(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters)
    {
        Routes = routes;
        Clusters = clusters;
        ChangeToken = new CancellationChangeToken(_cts.Token);
    }

    public IReadOnlyList<RouteConfig> Routes { get; }
    public IReadOnlyList<ClusterConfig> Clusters { get; }
    public IChangeToken ChangeToken { get; }

    internal void SignalChange() => _cts.Cancel();
}
