using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Net.Http.Headers;

using Yarp.ReverseProxy;
using Yarp.ReverseProxy.Health;
using Yarp.ReverseProxy.Model;

namespace West94.ProxyManager.Endpoints;

/// <summary>
/// Live health of one proxy host's <c>primary</c> destination. <see cref="Active"/> and
/// <see cref="Passive"/> are null when that check is disabled.
/// </summary>
public sealed record HealthStateDto(Guid ProxyHostId, string Status, string? Active, string? Passive, DateTimeOffset CheckedAt);

/// <summary>Combines the per-check states into one overall status.</summary>
public static class HealthStateMapper
{
    /// <summary>
    /// Unhealthy if any enabled check is Unhealthy; otherwise Healthy if any is Healthy; otherwise
    /// Unknown. A null argument means that check is disabled. A single Healthy check is enough because
    /// passive state stays Unknown on a host with little traffic, and HealthyAndUnknown still routes
    /// to Unknown destinations, so Unknown here would falsely suggest a problem.
    /// </summary>
    public static DestinationHealth Combine(DestinationHealth? active, DestinationHealth? passive)
    {
        DestinationHealth[] enabled = [.. new[] { active, passive }.OfType<DestinationHealth>()];

        if (enabled.Contains(DestinationHealth.Unhealthy))
            return DestinationHealth.Unhealthy;

        return enabled.Contains(DestinationHealth.Healthy)
            ? DestinationHealth.Healthy
            : DestinationHealth.Unknown;
    }
}

public static class HealthStateEndpoints
{
    /// <summary>
    /// Maps <c>GET /manage/api/health-states</c>. YARP keeps destination health only in this process,
    /// so the proxy serves it itself (ADR 0003). As a minimal-API endpoint it has Order 0 and wins over
    /// the YARP <c>ui-api-route</c> (Order 1) for this one path.
    /// </summary>
    public static IEndpointRouteBuilder MapHealthStateEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/manage/api/health-states", Ok<HealthStateDto[]> (IProxyStateLookup proxyState, HttpResponse response) =>
        {
            response.Headers[HeaderNames.CacheControl] = "no-store";
            var now = DateTimeOffset.UtcNow;

            var states = proxyState.GetClusters()
                .Where(c => Guid.TryParse(c.ClusterId, out _) && HasEnabledCheck(c))
                .Select(c => ToDto(c, now))
                .OfType<HealthStateDto>()
                .ToArray();

            return TypedResults.Ok(states);
        })
        .RequireAuthorization("AuthenticatedUsersOnly")
        .ExcludeFromDescription();

        return app;
    }

    private static bool HasEnabledCheck(ClusterState cluster)
    {
        var health = cluster.Model.Config.HealthCheck;
        return health?.Active?.Enabled == true || health?.Passive?.Enabled == true;
    }

    private static HealthStateDto? ToDto(ClusterState cluster, DateTimeOffset checkedAt)
    {
        if (!cluster.Destinations.TryGetValue("primary", out var destination))
            return null;

        var config = cluster.Model.Config.HealthCheck!;
        DestinationHealth? active = config.Active?.Enabled == true ? destination.Health.Active : null;
        DestinationHealth? passive = config.Passive?.Enabled == true ? destination.Health.Passive : null;

        return new HealthStateDto(
            Guid.Parse(cluster.ClusterId),
            HealthStateMapper.Combine(active, passive).ToString(),
            active?.ToString(),
            passive?.ToString(),
            checkedAt);
    }
}
