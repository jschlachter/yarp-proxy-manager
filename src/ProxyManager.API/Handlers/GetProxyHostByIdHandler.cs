using West94.ProxyManager.Core.AggregatesModel.ProxyHostAggregate;
using West94.ProxyManager.Core.DTOs;
using West94.ProxyManager.Core.Messages.Queries;

namespace West94.ProxyManager.API.Handlers;

public sealed class GetProxyHostByIdHandler(IProxyHostRepository repository)
{
    public async Task<ProxyHostDto?> Handle(GetProxyHostByIdQuery query, CancellationToken ct)
    {
        var host = await repository.FindAsync(query.Id, ct);
        return host is null ? null : GetProxyHostsHandler.MapToDto(host);
    }
}
