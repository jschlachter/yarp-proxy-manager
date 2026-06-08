using System.Collections.Concurrent;

using West94.ProxyManager.Core.AggregatesModel.AuditLogAggregate;

namespace West94.ProxyManager.Infrastructure.Repositories;

public sealed class InMemoryAuditLogRepository : IAuditLogRepository
{
    private readonly ConcurrentQueue<AuditLogEntry> _store = new();

    public Task AppendAsync(AuditLogEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _store.Enqueue(entry);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuditLogEntry>> GetByProxyHostAsync(
        Guid proxyHostId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        IReadOnlyList<AuditLogEntry> result = _store
            .Where(e => e.ProxyHostId == proxyHostId)
            .Where(e => from == null || e.OccurredAt >= from)
            .Where(e => to == null || e.OccurredAt <= to)
            .OrderByDescending(e => e.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<AuditLogEntry>> GetAllAsync(int page, int pageSize, CancellationToken ct = default)
    {
        IReadOnlyList<AuditLogEntry> result = _store
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Task.FromResult(result);
    }

    public Task<int> PurgeOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct = default)
    {
        // In-memory implementation: no-op (data is ephemeral)
        return Task.FromResult(0);
    }
}
