namespace West94.ProxyManager.Core.AggregatesModel.CertificateAggregate;

public interface ICertificateRepository
{
    Task<Certificate?> FindAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Certificate>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(Certificate certificate, CancellationToken ct = default);
    Task UpdateAsync(Certificate certificate, CancellationToken ct = default);
    Task RemoveAsync(Guid id, CancellationToken ct = default);
}
