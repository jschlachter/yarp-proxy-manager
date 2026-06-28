using Microsoft.EntityFrameworkCore;
using West94.ProxyManager.Core.AggregatesModel.AuditLogAggregate;
using West94.ProxyManager.Infrastructure.Data;

namespace West94.ProxyManager.Infrastructure.Repositories;

/// <summary>PostgreSQL-backed repository for AuditLogEntry records.</summary>
public sealed class PostgresAuditLogRepository(ProxyManagerDbContext db) : IAuditLogRepository
{
    public async Task AppendAsync(AuditLogEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        db.AuditLogEntries.Add(entry);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AuditLogEntry>> GetByProxyHostAsync(
        Guid proxyHostId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        IQueryable<AuditLogEntry> query = db.AuditLogEntries
            .AsNoTracking()
            .Where(e => e.ProxyHostId == proxyHostId);

        if (from is not null)
            query = query.Where(e => e.OccurredAt >= from);
        if (to is not null)
            query = query.Where(e => e.OccurredAt <= to);

        return await query
            .OrderByDescending(e => e.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AuditLogEntry>> GetAllAsync(
        int page, int pageSize, CancellationToken ct = default) =>
        await db.AuditLogEntries
            .AsNoTracking()
            .OrderByDescending(e => e.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

    public async Task<int> PurgeOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct = default) =>
        await db.AuditLogEntries
            .Where(e => e.OccurredAt < cutoff)
            .ExecuteDeleteAsync(ct);
}
