namespace West94.ProxyManager.Core.AggregatesModel.AuditLogAggregate;

public interface IAuditLogRepository
{
    Task AppendAsync(AuditLogEntry entry, CancellationToken ct = default);

    Task<IReadOnlyList<AuditLogEntry>> GetByProxyHostAsync(
        Guid proxyHostId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default);

    Task<IReadOnlyList<AuditLogEntry>> GetAllAsync(int page, int pageSize, CancellationToken ct = default);

    /// <summary>Deletes all entries with OccurredAt before <paramref name="cutoff"/>. Returns the count of deleted rows.</summary>
    Task<int> PurgeOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct = default);
}
