using West94.ProxyManager.Core.AggregatesModel.ProxyHostAggregate;
using Yarp.ReverseProxy.Configuration;

namespace West94.ProxyManager.Yarp;

/// <summary>Maps <see cref="ProxyHost"/> domain objects to YARP route and cluster configuration.</summary>
public static class ProxyHostYarpTranslator
{
    public static (IReadOnlyList<RouteConfig> Routes, IReadOnlyList<ClusterConfig> Clusters)
        Translate(IEnumerable<ProxyHost> hosts)
    {
        var routes = new List<RouteConfig>();
        var clusters = new List<ClusterConfig>();

        foreach (var host in hosts)
        {
            if (!host.IsEnabled) continue;

            var id = host.Id.ToString();

            routes.Add(new RouteConfig
            {
                RouteId = id,
                ClusterId = id,
                Match = new RouteMatch
                {
                    Hosts = host.DomainNames.ToList(),
                    Path = "/{**catch-all}"
                },
                Order = 100
            });

            clusters.Add(new ClusterConfig
            {
                ClusterId = id,
                Destinations = new Dictionary<string, DestinationConfig>
                {
                    ["primary"] = new DestinationConfig { Address = host.Destination.ToString() }
                }
            });
        }

        return (routes, clusters);
    }
}
