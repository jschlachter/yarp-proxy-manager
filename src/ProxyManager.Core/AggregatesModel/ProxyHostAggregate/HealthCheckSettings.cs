namespace West94.ProxyManager.Core.AggregatesModel.ProxyHostAggregate;

/// <summary>Built-in YARP active health policies. Values are YARP's registered policy names.</summary>
public enum ActiveHealthCheckPolicy { ConsecutiveFailures }

/// <summary>Built-in YARP passive health policies. Values are YARP's registered policy names.</summary>
public enum PassiveHealthCheckPolicy { TransportFailureRate }

/// <summary>
/// How YARP picks destinations once health is known. <see cref="HealthyAndUnknown"/> drops an
/// unhealthy destination (a single-destination host then returns 503); <see cref="HealthyOrPanic"/>
/// falls back to all destinations when none is healthy.
/// </summary>
public enum AvailableDestinationsPolicy { HealthyAndUnknown, HealthyOrPanic }

/// <summary>
/// YARP health check configuration for a proxy host (ADR 0003). A present check is enabled; a null
/// check is disabled. At least one check must be present — a host without checks stores no settings.
/// </summary>
public sealed record HealthCheckSettings
{
    public ActiveHealthCheck? Active { get; }
    public PassiveHealthCheck? Passive { get; }
    public AvailableDestinationsPolicy AvailableDestinationsPolicy { get; }

    public HealthCheckSettings(ActiveHealthCheck? active, PassiveHealthCheck? passive, AvailableDestinationsPolicy availableDestinationsPolicy)
    {
        if (active is null && passive is null)
            throw new ArgumentException("At least one health check must be enabled.", nameof(active));

        Active = active;
        Passive = passive;
        AvailableDestinationsPolicy = availableDestinationsPolicy;
    }
}

/// <summary>Active (probing) health check. Null tuning fields fall back to YARP's defaults.</summary>
public sealed record ActiveHealthCheck
{
    public ActiveHealthCheckPolicy Policy { get; }
    public TimeSpan? Interval { get; }
    public TimeSpan? Timeout { get; }
    public string? Path { get; }
    public string? Query { get; }
    public string? HealthAddress { get; }
    public int? ConsecutiveFailuresThreshold { get; }

    public ActiveHealthCheck(
        ActiveHealthCheckPolicy policy,
        TimeSpan? interval = null,
        TimeSpan? timeout = null,
        string? path = null,
        string? query = null,
        string? healthAddress = null,
        int? consecutiveFailuresThreshold = null)
    {
        HealthCheckGuard.Positive(interval, nameof(interval));
        HealthCheckGuard.Positive(timeout, nameof(timeout));

        if (path is not null && !path.StartsWith('/'))
            throw new ArgumentException("Path must start with '/'.", nameof(path));

        if (query is not null && !query.StartsWith('?'))
            throw new ArgumentException("Query must start with '?'.", nameof(query));

        if (healthAddress is not null
            && (!Uri.TryCreate(healthAddress, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
            throw new ArgumentException("Health address must be an absolute http or https URI.", nameof(healthAddress));

        if (consecutiveFailuresThreshold is < 1)
            throw new ArgumentOutOfRangeException(nameof(consecutiveFailuresThreshold), consecutiveFailuresThreshold, "Threshold must be at least 1.");

        Policy = policy;
        Interval = interval;
        Timeout = timeout;
        Path = path;
        Query = query;
        HealthAddress = healthAddress;
        ConsecutiveFailuresThreshold = consecutiveFailuresThreshold;
    }
}

/// <summary>Passive (traffic-observing) health check. Null tuning fields fall back to YARP's defaults.</summary>
public sealed record PassiveHealthCheck
{
    public PassiveHealthCheckPolicy Policy { get; }
    public TimeSpan? ReactivationPeriod { get; }
    public double? FailureRateLimit { get; }

    public PassiveHealthCheck(PassiveHealthCheckPolicy policy, TimeSpan? reactivationPeriod = null, double? failureRateLimit = null)
    {
        HealthCheckGuard.Positive(reactivationPeriod, nameof(reactivationPeriod));

        if (failureRateLimit is <= 0 or >= 1)
            throw new ArgumentOutOfRangeException(nameof(failureRateLimit), failureRateLimit, "Failure rate limit must be between 0 and 1 (exclusive).");

        Policy = policy;
        ReactivationPeriod = reactivationPeriod;
        FailureRateLimit = failureRateLimit;
    }
}

file static class HealthCheckGuard
{
    public static void Positive(TimeSpan? value, string paramName)
    {
        if (value is { } v && v <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(paramName, value, "Duration must be greater than zero.");
    }
}
