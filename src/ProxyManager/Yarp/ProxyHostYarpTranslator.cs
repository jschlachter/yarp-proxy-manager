using System.Globalization;

using West94.ProxyManager.Core.AggregatesModel.ProxyHostAggregate;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Health;

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

            var health = host.HealthCheck;

            clusters.Add(new ClusterConfig
            {
                ClusterId = id,
                HealthCheck = ToHealthCheckConfig(health),
                Metadata = ToHealthMetadata(health),
                Destinations = new Dictionary<string, DestinationConfig>
                {
                    ["primary"] = new DestinationConfig
                    {
                        Address = host.Destination.ToString(),
                        Health = health?.Active?.HealthAddress
                    }
                }
            });
        }

        return (routes, clusters);
    }

    /// <summary>
    /// Maps the host's settings to YARP's cluster health config. Null tuning fields stay null so YARP
    /// applies its own defaults. Policy enum names are YARP's registered policy names.
    /// </summary>
    private static HealthCheckConfig? ToHealthCheckConfig(HealthCheckSettings? settings) => settings is null ? null : new()
    {
        AvailableDestinationsPolicy = settings.AvailableDestinationsPolicy.ToString(),
        Active = settings.Active is { } a
            ? new ActiveHealthCheckConfig
            {
                Enabled = true,
                Interval = a.Interval,
                Timeout = a.Timeout,
                Policy = a.Policy.ToString(),
                Path = a.Path,
                Query = a.Query
            }
            : null,
        Passive = settings.Passive is { } p
            ? new PassiveHealthCheckConfig
            {
                Enabled = true,
                Policy = p.Policy.ToString(),
                ReactivationPeriod = p.ReactivationPeriod
            }
            : null
    };

    /// <summary>Per-cluster policy parameters, which YARP reads from cluster metadata.</summary>
    private static Dictionary<string, string>? ToHealthMetadata(HealthCheckSettings? settings)
    {
        var metadata = new Dictionary<string, string>();

        if (settings?.Active?.ConsecutiveFailuresThreshold is { } threshold)
            metadata[ConsecutiveFailuresHealthPolicyOptions.ThresholdMetadataName] = threshold.ToString(CultureInfo.InvariantCulture);

        // YARP parses the rate with the invariant culture, so a de-DE "0,3" would be misread.
        if (settings?.Passive?.FailureRateLimit is { } rate)
            metadata[TransportFailureRateHealthPolicyOptions.FailureRateLimitMetadataName] = rate.ToString(CultureInfo.InvariantCulture);

        return metadata.Count == 0 ? null : metadata;
    }
}
