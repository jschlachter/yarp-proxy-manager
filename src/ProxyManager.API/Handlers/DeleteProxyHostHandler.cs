using System.Text.Json;

using West94.ProxyManager.Core.AggregatesModel.AuditLogAggregate;
using West94.ProxyManager.Core.AggregatesModel.ProxyHostAggregate;
using West94.ProxyManager.Core.Exceptions;
using West94.ProxyManager.Core.Messages.Commands;
using West94.ProxyManager.Core.Messages.Events;

namespace West94.ProxyManager.API.Handlers;

public sealed class DeleteProxyHostHandler(IProxyHostRepository repository, IAuditLogRepository auditLog)
{
    public async Task<ProxyHostDeletedEvent> Handle(DeleteProxyHostCommand command, CancellationToken ct)
    {
        var host = await repository.FindAsync(command.Id, ct)
            ?? throw new ProxyHostNotFoundException(command.Id);

        var previousState = JsonSerializer.Serialize(GetProxyHostsHandler.MapToDto(host));
        var domainNames = host.DomainNames.ToList();

        await repository.RemoveAsync(host.Id, ct);

        await auditLog.AppendAsync(
            AuditLogEntry.Create(command.ActorId, AuditOperation.Deleted, host.Id, previousState, null), ct);

        return new ProxyHostDeletedEvent(host.Id, domainNames, DateTimeOffset.UtcNow);
    }
}
