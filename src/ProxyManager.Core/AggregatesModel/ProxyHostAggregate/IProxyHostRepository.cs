namespace West94.ProxyManager.Core.AggregatesModel.ProxyHostAggregate;

public interface IProxyHostRepository
{
    Task<ProxyHost?> FindAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ProxyHost>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(ProxyHost host, CancellationToken ct = default);
    Task UpdateAsync(ProxyHost host, CancellationToken ct = default);
    Task RemoveAsync(Guid id, CancellationToken ct = default);
}
