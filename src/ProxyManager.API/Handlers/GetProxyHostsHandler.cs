using West94.ProxyManager.Core.AggregatesModel.ProxyHostAggregate;
using West94.ProxyManager.Core.DTOs;
using West94.ProxyManager.Core.Messages.Queries;

namespace West94.ProxyManager.API.Handlers;

public sealed class GetProxyHostsHandler(IProxyHostRepository repository)
{
    public async Task<PagedResult<ProxyHostDto>> Handle(GetProxyHostsQuery query, CancellationToken ct)
    {
        var all = await repository.GetAllAsync(ct);
        var sorted = all.OrderBy(h => h.DomainNames.FirstOrDefault()).ToList();
        var totalCount = sorted.Count;
        var items = sorted
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(MapToDto)
            .ToList();

        return new PagedResult<ProxyHostDto>(items, totalCount, query.Page, query.PageSize);
    }

    internal static ProxyHostDto MapToDto(ProxyHost host) => new(
        host.Id,
        host.DomainNames,
        host.Destination.ToString(),
        host.IsEnabled,
        host.Certificate is null
            ? null
            : new ProxyCertificateDto(host.Certificate.CertificatePath, host.Certificate.KeyPath));
}
