using Microsoft.Extensions.Options;

using West94.ProxyManager.Options;
using Yarp.ReverseProxy.Configuration;

namespace West94.ProxyManager.Yarp;

/// <summary>
/// Scopes every route without <c>Match.Hosts</c> to <see cref="ManagementOptions.Hosts"/> (ADR 0005).
/// Only system routes from proxysettings lack hosts, because <see cref="ProxyHostYarpTranslator"/>
/// always sets a user route's domains; without this, a host-less system route would shadow user
/// routes on <c>/api</c>, <c>/manage</c> (and <c>/**</c> in Development) on every domain.
/// </summary>
public sealed class ManagementHostRouteFilter(IOptions<ManagementOptions> options) : IProxyConfigFilter
{
    /// <summary>Leaves clusters unchanged.</summary>
    public ValueTask<ClusterConfig> ConfigureClusterAsync(ClusterConfig cluster, CancellationToken cancel)
        => ValueTask.FromResult(cluster);

    /// <summary>Adds the management hosts to a route that has none; keeps a route's own hosts.</summary>
    public ValueTask<RouteConfig> ConfigureRouteAsync(RouteConfig route, ClusterConfig? cluster, CancellationToken cancel)
    {
        if (route.Match.Hosts is { Count: > 0 })
            return ValueTask.FromResult(route);

        return ValueTask.FromResult(route with { Match = route.Match with { Hosts = options.Value.Hosts } });
    }
}
