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

    public Task<IReadOnlyList<AuditLogEntry>> GetByProxyHostAsync(Guid proxyHostId, CancellationToken ct = default)
    {
        IReadOnlyList<AuditLogEntry> result = _store
            .Where(e => e.ProxyHostId == proxyHostId)
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
}
