using West94.ProxyManager.Core.AggregatesModel.CertificateAggregate;
using West94.ProxyManager.Core.DTOs;
using West94.ProxyManager.Core.Messages.Queries;

namespace West94.ProxyManager.API.Handlers;

public sealed class GetCertificatesHandler(ICertificateRepository repository)
{
    public async Task<PagedResult<CertificateDto>> Handle(GetCertificatesQuery query, CancellationToken ct)
    {
        var all = await repository.GetAllAsync(ct);
        var sorted = all.OrderBy(c => c.Name).ToList();
        var items = sorted
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(MapToDto)
            .ToList();

        return new PagedResult<CertificateDto>(items, sorted.Count, query.Page, query.PageSize);
    }

    internal static CertificateDto MapToDto(Certificate c) => new(
        c.Id, c.Name, c.Format.ToString(), c.CertificatePath, c.KeyFilePath, c.CreatedAt, c.UpdatedAt);
}
