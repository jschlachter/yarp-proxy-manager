using West94.ProxyManager.Core.AggregatesModel.AuditLogAggregate;

namespace West94.ProxyManager.API.Tests.Unit.Fakes;

internal sealed class FakeAuditLogRepository : IAuditLogRepository
{
    public List<AuditLogEntry> Entries { get; } = [];

    public Task AppendAsync(AuditLogEntry entry, CancellationToken ct = default)
    {
        Entries.Add(entry);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuditLogEntry>> GetByProxyHostAsync(
        Guid proxyHostId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<AuditLogEntry>>(
            Entries
                .Where(e => e.ProxyHostId == proxyHostId)
                .Where(e => from == null || e.OccurredAt >= from)
                .Where(e => to == null || e.OccurredAt <= to)
                .OrderByDescending(e => e.OccurredAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList());

    public Task<IReadOnlyList<AuditLogEntry>> GetAllAsync(int page, int pageSize, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<AuditLogEntry>>(Entries.Skip((page - 1) * pageSize).Take(pageSize).ToList());

    public Task<int> PurgeOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct = default) =>
        Task.FromResult(0);
}
