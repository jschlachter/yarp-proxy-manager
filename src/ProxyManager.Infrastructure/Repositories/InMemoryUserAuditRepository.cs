using System.Collections.Concurrent;
using West94.ProxyManager.Core.AggregatesModel.UserAggregate;
using West94.ProxyManager.Core.DTOs;

namespace West94.ProxyManager.Infrastructure.Repositories;

/// <summary>Thread-safe in-memory implementation of <see cref="IUserAuditRepository"/>.</summary>
public sealed class InMemoryUserAuditRepository : IUserAuditRepository
{
    private readonly ConcurrentQueue<UserAuditEntry> _queue = new();

    /// <inheritdoc/>
    public Task AppendAsync(UserAuditEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        _queue.Enqueue(entry);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<PagedResult<UserAuditEntry>> QueryAsync(
        string? subFilter,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = _queue.AsEnumerable();

        if (subFilter is not null)
            query = query.Where(e => e.SubjectSub == subFilter);

        if (from.HasValue)
            query = query.Where(e => e.OccurredAt >= from.Value);

        if (to.HasValue)
            query = query.Where(e => e.OccurredAt <= to.Value);

        var ordered = query.OrderBy(e => e.OccurredAt).ToList();
        var total = ordered.Count;
        var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Task.FromResult(new PagedResult<UserAuditEntry>(items, total, page, pageSize));
    }
}
