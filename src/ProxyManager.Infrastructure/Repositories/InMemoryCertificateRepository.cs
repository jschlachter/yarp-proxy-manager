using System.Collections.Concurrent;
using West94.ProxyManager.Core.AggregatesModel.CertificateAggregate;

namespace West94.ProxyManager.Infrastructure.Repositories;

public sealed class InMemoryCertificateRepository : ICertificateRepository
{
    private readonly ConcurrentDictionary<Guid, Certificate> _store = new();

    public Task<Certificate?> FindAsync(Guid id, CancellationToken ct = default)
    {
        _store.TryGetValue(id, out var cert);
        return Task.FromResult(cert);
    }

    public Task<IReadOnlyList<Certificate>> GetAllAsync(CancellationToken ct = default)
    {
        IReadOnlyList<Certificate> result = _store.Values.ToList();
        return Task.FromResult(result);
    }

    public Task AddAsync(Certificate certificate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(certificate);

        if (!_store.TryAdd(certificate.Id, certificate))
            throw new InvalidOperationException($"A Certificate with id '{certificate.Id}' already exists.");

        return Task.CompletedTask;
    }

    public Task UpdateAsync(Certificate certificate, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(certificate);

        if (!_store.ContainsKey(certificate.Id))
            throw new InvalidOperationException($"Certificate with id '{certificate.Id}' was not found.");

        _store[certificate.Id] = certificate;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(Guid id, CancellationToken ct = default)
    {
        _store.TryRemove(id, out _);
        return Task.CompletedTask;
    }
}
