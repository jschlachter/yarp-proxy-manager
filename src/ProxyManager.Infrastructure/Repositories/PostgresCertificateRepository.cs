using Microsoft.EntityFrameworkCore;
using West94.ProxyManager.Core.AggregatesModel.CertificateAggregate;
using West94.ProxyManager.Infrastructure.Data;

namespace West94.ProxyManager.Infrastructure.Repositories;

/// <summary>PostgreSQL-backed repository for Certificate aggregates.</summary>
public sealed class PostgresCertificateRepository(ProxyManagerDbContext db) : ICertificateRepository
{
    public async Task<Certificate?> FindAsync(Guid id, CancellationToken ct = default)
    {
        var record = await db.Certificates.FindAsync([id], ct);
        return record is null ? null : ToDomain(record);
    }

    public async Task<IReadOnlyList<Certificate>> GetAllAsync(CancellationToken ct = default)
    {
        var records = await db.Certificates.AsNoTracking().ToListAsync(ct);
        return records.ConvertAll(ToDomain);
    }

    public async Task AddAsync(Certificate certificate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        db.Certificates.Add(ToRecord(certificate));
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Certificate certificate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        var existing = await db.Certificates.FindAsync([certificate.Id], ct)
            ?? throw new InvalidOperationException($"Certificate '{certificate.Id}' not found.");

        existing.Name = certificate.Name;
        existing.PassPhrase = certificate.PassPhrase;
        existing.UpdatedAt = certificate.UpdatedAt;

        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(Guid id, CancellationToken ct = default)
    {
        var record = await db.Certificates.FindAsync([id], ct);
        if (record is null) return;

        db.Certificates.Remove(record);
        await db.SaveChangesAsync(ct);
    }

    private static Certificate ToDomain(CertificateRecord r) =>
        Certificate.Reconstitute(r.Id, r.Name, (CertificateFormat)r.Format,
            r.CertificatePath, r.KeyFilePath, r.PassPhrase, r.CreatedAt, r.UpdatedAt);

    private static CertificateRecord ToRecord(Certificate c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        Format = (int)c.Format,
        CertificatePath = c.CertificatePath,
        KeyFilePath = c.KeyFilePath,
        PassPhrase = c.PassPhrase,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt
    };
}
