using System.Text.Json;

using West94.ProxyManager.Core.AggregatesModel.AuditLogAggregate;
using West94.ProxyManager.Core.AggregatesModel.ProxyHostAggregate;
using West94.ProxyManager.Core.DTOs;
using West94.ProxyManager.Core.Exceptions;
using West94.ProxyManager.Core.Messages.Commands;
using West94.ProxyManager.Core.Messages.Events;

namespace West94.ProxyManager.API.Handlers;

public sealed class UpdateProxyHostHandler(IProxyHostRepository repository, IAuditLogRepository auditLog)
{
    public async Task<(ProxyHostDto, ProxyHostUpdatedEvent)> Handle(UpdateProxyHostCommand command, CancellationToken ct)
    {
        var host = await repository.FindAsync(command.Id, ct)
            ?? throw new ProxyHostNotFoundException(command.Id);

        var previousState = JsonSerializer.Serialize(GetProxyHostsHandler.MapToDto(host));

        if (command.DomainNames is not null)
            host.UpdateDomainNames(command.DomainNames);

        if (command.DestinationUri is not null)
        {
            if (!Uri.TryCreate(command.DestinationUri, UriKind.Absolute, out var parsed)
                || (parsed.Scheme != "http" && parsed.Scheme != "https"))
                throw new ProxyHostValidationException(
                    $"'{command.DestinationUri}' must be a valid absolute http or https URI.");

            host.UpdateDestination(DestinationUri.Parse(command.DestinationUri));
        }

        if (command.IsEnabled.HasValue)
        {
            if (command.IsEnabled.Value) host.Enable();
            else host.Disable();
        }

        if (command.CertificatePath is not null || command.CertificateKeyPath is not null)
        {
            var cert = command.CertificatePath is not null
                ? new ProxyCertificate(command.CertificatePath, command.CertificateKeyPath)
                : null;
            host.SetCertificate(cert);
        }

        await repository.UpdateAsync(host, ct);

        var dto = GetProxyHostsHandler.MapToDto(host);
        var newState = JsonSerializer.Serialize(dto);

        await auditLog.AppendAsync(
            AuditLogEntry.Create(command.ActorId, AuditOperation.Updated, host.Id, previousState, newState), ct);

        var @event = new ProxyHostUpdatedEvent(
            host.Id,
            host.DomainNames.ToList(),
            host.Destination.ToString(),
            host.IsEnabled,
            DateTimeOffset.UtcNow);

        return (dto, @event);
    }
}
