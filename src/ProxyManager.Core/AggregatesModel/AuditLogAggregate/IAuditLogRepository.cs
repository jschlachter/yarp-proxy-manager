namespace West94.ProxyManager.Core.AggregatesModel.AuditLogAggregate;

public interface IAuditLogRepository
{
    Task AppendAsync(AuditLogEntry entry, CancellationToken ct = default);
    Task<IReadOnlyList<AuditLogEntry>> GetByProxyHostAsync(Guid proxyHostId, CancellationToken ct = default);
    Task<IReadOnlyList<AuditLogEntry>> GetAllAsync(int page, int pageSize, CancellationToken ct = default);
}
