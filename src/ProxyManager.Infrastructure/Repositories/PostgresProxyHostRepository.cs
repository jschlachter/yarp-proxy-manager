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
        existing.CertificatePath = host.Certificate?.CertificatePath;
        existing.CertificateKeyPath = host.Certificate?.KeyPath;
        existing.CertificatePassword = host.Certificate?.Password;

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
        ProxyCertificate? cert = r.CertificatePath is not null
            ? new ProxyCertificate(r.CertificatePath, r.CertificateKeyPath, r.CertificatePassword)
            : null;
        return ProxyHost.Reconstitute(r.Id, r.DomainNames, destination, r.IsEnabled, cert);
    }

    private static ProxyHostRecord ToRecord(ProxyHost h) => new()
    {
        Id = h.Id,
        DomainNames = h.DomainNames.ToList(),
        DestinationScheme = h.Destination.Scheme,
        DestinationHost = h.Destination.Host,
        DestinationPort = h.Destination.Port,
        IsEnabled = h.IsEnabled,
        CertificatePath = h.Certificate?.CertificatePath,
        CertificateKeyPath = h.Certificate?.KeyPath,
        CertificatePassword = h.Certificate?.Password
    };
}
