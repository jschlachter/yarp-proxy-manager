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

    public Task<IReadOnlyList<AuditLogEntry>> GetByProxyHostAsync(Guid proxyHostId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<AuditLogEntry>>(Entries.Where(e => e.ProxyHostId == proxyHostId).ToList());

    public Task<IReadOnlyList<AuditLogEntry>> GetAllAsync(int page, int pageSize, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<AuditLogEntry>>(Entries.Skip((page - 1) * pageSize).Take(pageSize).ToList());
}
