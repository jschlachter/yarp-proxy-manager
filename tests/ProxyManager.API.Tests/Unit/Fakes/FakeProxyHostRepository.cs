using West94.ProxyManager.Core.AggregatesModel.ProxyHostAggregate;

namespace West94.ProxyManager.API.Tests.Unit.Fakes;

internal sealed class FakeProxyHostRepository : IProxyHostRepository
{
    private readonly List<ProxyHost> _hosts = [];

    public void Seed(params ProxyHost[] hosts) => _hosts.AddRange(hosts);

    public Task<ProxyHost?> FindAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_hosts.FirstOrDefault(h => h.Id == id));

    public Task<IReadOnlyList<ProxyHost>> GetAllAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ProxyHost>>(_hosts.ToList());

    public Task AddAsync(ProxyHost host, CancellationToken ct = default)
    {
        _hosts.Add(host);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(ProxyHost host, CancellationToken ct = default) => Task.CompletedTask;

    public Task RemoveAsync(Guid id, CancellationToken ct = default)
    {
        _hosts.RemoveAll(h => h.Id == id);
        return Task.CompletedTask;
    }
}
