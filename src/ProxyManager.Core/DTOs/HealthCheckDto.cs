namespace West94.ProxyManager.Core.DTOs;

/// <summary>
/// Wire shape of a proxy host's health checks (ADR 0003). Policies are strings and durations are whole
/// seconds. A null <see cref="Active"/> or <see cref="Passive"/> means that check is off; a null tuning
/// field means YARP's default applies.
/// </summary>
public sealed record HealthCheckDto(
    string AvailableDestinationsPolicy,
    ActiveHealthCheckDto? Active,
    PassiveHealthCheckDto? Passive);

/// <summary>Active health check settings on the wire.</summary>
public sealed record ActiveHealthCheckDto(
    string Policy,
    int? IntervalSeconds = null,
    int? TimeoutSeconds = null,
    string? Path = null,
    string? Query = null,
    string? HealthAddress = null,
    int? ConsecutiveFailuresThreshold = null);

/// <summary>Passive health check settings on the wire.</summary>
public sealed record PassiveHealthCheckDto(
    string Policy,
    int? ReactivationPeriodSeconds = null,
    double? FailureRateLimit = null);
