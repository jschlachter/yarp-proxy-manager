using West94.ProxyManager.Core.AggregatesModel.CertificateAggregate;

namespace West94.ProxyManager.API.Tests.Unit.Fakes;

internal sealed class FakeCertificateRepository : ICertificateRepository
{
    private readonly List<Certificate> _certs = [];

    public void Seed(params Certificate[] certs) => _certs.AddRange(certs);

    public Task<Certificate?> FindAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_certs.FirstOrDefault(c => c.Id == id));

    public Task<IReadOnlyList<Certificate>> GetAllAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Certificate>>(_certs.ToList());

    public Task AddAsync(Certificate certificate, CancellationToken ct = default)
    {
        _certs.Add(certificate);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Certificate certificate, CancellationToken ct = default) => Task.CompletedTask;

    public Task RemoveAsync(Guid id, CancellationToken ct = default)
    {
        _certs.RemoveAll(c => c.Id == id);
        return Task.CompletedTask;
    }
}
