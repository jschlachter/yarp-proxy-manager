using West94.ProxyManager.Core.AggregatesModel.ProxyHostAggregate;

namespace West94.ProxyManager.Infrastructure.Data;

/// <summary>EF Core persistence model for ProxyHost. Decouples the ORM from the domain aggregate.</summary>
internal sealed class ProxyHostRecord
{
    public Guid Id { get; set; }
    public List<string> DomainNames { get; set; } = [];
    public string DestinationScheme { get; set; } = string.Empty;
    public string DestinationHost { get; set; } = string.Empty;
    public int DestinationPort { get; set; }
    public bool IsEnabled { get; set; }
    public Guid? CertificateId { get; set; }
    public TlsMode TlsMode { get; set; }
    public HealthCheckRecord? HealthCheck { get; set; }
}

/// <summary>
/// jsonb persistence shape of <see cref="HealthCheckSettings"/>. Durations are whole seconds and enums
/// are strings so the stored JSON stays readable and stable if enum ordinals ever change.
/// </summary>
internal sealed record HealthCheckRecord(
    ActiveHealthCheckRecord? Active,
    PassiveHealthCheckRecord? Passive,
    string AvailableDestinationsPolicy);

internal sealed record ActiveHealthCheckRecord(
    string Policy,
    int? IntervalSeconds,
    int? TimeoutSeconds,
    string? Path,
    string? Query,
    string? HealthAddress,
    int? ConsecutiveFailuresThreshold);

internal sealed record PassiveHealthCheckRecord(
    string Policy,
    int? ReactivationPeriodSeconds,
    double? FailureRateLimit);
