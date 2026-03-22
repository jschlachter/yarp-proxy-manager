using System.Collections.Concurrent;
using West94.ProxyManager.Core.AggregatesModel.ProxyHostAggregate;

namespace West94.ProxyManager.Infrastructure.Repositories;

public sealed class InMemoryProxyHostRepository : IProxyHostRepository
{
    private readonly ConcurrentDictionary<Guid, ProxyHost> _store = new();

    public Task<ProxyHost?> FindAsync(Guid id, CancellationToken ct = default)
    {
        _store.TryGetValue(id, out var host);
        return Task.FromResult(host);
    }

    public Task<IReadOnlyList<ProxyHost>> GetAllAsync(CancellationToken ct = default)
    {
        IReadOnlyList<ProxyHost> result = _store.Values.ToList();
        return Task.FromResult(result);
    }

    public Task AddAsync(ProxyHost host, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(host);

        if (!_store.TryAdd(host.Id, host))
            throw new InvalidOperationException($"A ProxyHost with id '{host.Id}' already exists.");

        return Task.CompletedTask;
    }

    public Task UpdateAsync(ProxyHost host, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(host);

        if (!_store.ContainsKey(host.Id))
            throw new InvalidOperationException($"ProxyHost with id '{host.Id}' was not found.");

        _store[host.Id] = host;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(Guid id, CancellationToken ct = default)
    {
        _store.TryRemove(id, out _);
        return Task.CompletedTask;
    }
}
