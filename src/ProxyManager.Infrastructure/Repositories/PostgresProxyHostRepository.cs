using Microsoft.EntityFrameworkCore;
using West94.ProxyManager.Core.AggregatesModel.ProxyHostAggregate;
using West94.ProxyManager.Infrastructure.Data;

namespace West94.ProxyManager.Infrastructure.Repositories;

/// <summary>PostgreSQL-backed repository for ProxyHost aggregates.</summary>
public sealed class PostgresProxyHostRepository(ProxyManagerDbContext db) : IProxyHostRepository
{
    public async Task<ProxyHost?> FindAsync(Guid id, CancellationToken ct = default)
    {
        var record = await db.ProxyHosts.FindAsync([id], ct);
        return record is null ? null : ToDomain(record);
    }

    public async Task<IReadOnlyList<ProxyHost>> GetAllAsync(CancellationToken ct = default)
    {
        var records = await db.ProxyHosts.AsNoTracking().ToListAsync(ct);
        return records.ConvertAll(ToDomain);
    }

    public async Task AddAsync(ProxyHost host, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        var record = ToRecord(host);
        db.ProxyHosts.Add(record);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(ProxyHost host, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(host);
        var existing = await db.ProxyHosts.FindAsync([host.Id], ct)
            ?? throw new InvalidOperationException($"ProxyHost '{host.Id}' not found.");

        existing.DomainNames = host.DomainNames.ToList();
        existing.DestinationScheme = host.Destination.Scheme;
        existing.DestinationHost = host.Destination.Host;
        existing.DestinationPort = host.Destination.Port;
        existing.IsEnabled = host.IsEnabled;
        existing.CertificateId = host.CertificateId;
        existing.TlsMode = host.TlsMode;
        existing.HealthCheck = ToRecord(host.HealthCheck);

        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(Guid id, CancellationToken ct = default)
    {
        var record = await db.ProxyHosts.FindAsync([id], ct);
        if (record is null) return;

        db.ProxyHosts.Remove(record);
        await db.SaveChangesAsync(ct);
    }

    private static ProxyHost ToDomain(ProxyHostRecord r)
    {
        var destination = new DestinationUri(r.DestinationScheme, r.DestinationHost, r.DestinationPort);
        return ProxyHost.Reconstitute(r.Id, r.DomainNames, destination, r.IsEnabled, r.CertificateId, r.TlsMode, ToDomain(r.HealthCheck));
    }

    private static ProxyHostRecord ToRecord(ProxyHost h) => new()
    {
        Id = h.Id,
        DomainNames = h.DomainNames.ToList(),
        DestinationScheme = h.Destination.Scheme,
        DestinationHost = h.Destination.Host,
        DestinationPort = h.Destination.Port,
        IsEnabled = h.IsEnabled,
        CertificateId = h.CertificateId,
        TlsMode = h.TlsMode,
        HealthCheck = ToRecord(h.HealthCheck)
    };

    private static HealthCheckSettings? ToDomain(HealthCheckRecord? r) => r is null ? null : new(
        r.Active is { } a
            ? new ActiveHealthCheck(
                Enum.Parse<ActiveHealthCheckPolicy>(a.Policy),
                Seconds(a.IntervalSeconds),
                Seconds(a.TimeoutSeconds),
                a.Path,
                a.Query,
                a.HealthAddress,
                a.ConsecutiveFailuresThreshold)
            : null,
        r.Passive is { } p
            ? new PassiveHealthCheck(
                Enum.Parse<PassiveHealthCheckPolicy>(p.Policy),
                Seconds(p.ReactivationPeriodSeconds),
                p.FailureRateLimit)
            : null,
        Enum.Parse<AvailableDestinationsPolicy>(r.AvailableDestinationsPolicy));

    private static HealthCheckRecord? ToRecord(HealthCheckSettings? h) => h is null ? null : new(
        h.Active is { } a
            ? new ActiveHealthCheckRecord(
                a.Policy.ToString(),
                WholeSeconds(a.Interval),
                WholeSeconds(a.Timeout),
                a.Path,
                a.Query,
                a.HealthAddress,
                a.ConsecutiveFailuresThreshold)
            : null,
        h.Passive is { } p
            ? new PassiveHealthCheckRecord(p.Policy.ToString(), WholeSeconds(p.ReactivationPeriod), p.FailureRateLimit)
            : null,
        h.AvailableDestinationsPolicy.ToString());

    private static TimeSpan? Seconds(int? seconds) => seconds is { } s ? TimeSpan.FromSeconds(s) : null;

    private static int? WholeSeconds(TimeSpan? value) => value is { } v ? (int)v.TotalSeconds : null;
}
