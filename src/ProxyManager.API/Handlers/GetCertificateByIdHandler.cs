using West94.ProxyManager.Core.AggregatesModel.CertificateAggregate;
using West94.ProxyManager.Core.DTOs;
using West94.ProxyManager.Core.Messages.Queries;

namespace West94.ProxyManager.API.Handlers;

public sealed class GetCertificateByIdHandler(ICertificateRepository repository)
{
    public async Task<CertificateDto?> Handle(GetCertificateByIdQuery query, CancellationToken ct)
    {
        var cert = await repository.FindAsync(query.Id, ct);
        return cert is null ? null : GetCertificatesHandler.MapToDto(cert);
    }
}
