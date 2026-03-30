using West94.ProxyManager.Core.AggregatesModel.AuditLogAggregate;
using West94.ProxyManager.Core.AggregatesModel.ProxyHostAggregate;
using West94.ProxyManager.Core.DTOs;
using West94.ProxyManager.Core.Exceptions;
using West94.ProxyManager.Core.Messages.Commands;
using West94.ProxyManager.Core.Messages.Events;

namespace West94.ProxyManager.API.Handlers;

public sealed class CreateProxyHostHandler(IProxyHostRepository repository, IAuditLogRepository auditLog)
{
    public async Task<(ProxyHostDto, ProxyHostCreatedEvent)> Handle(CreateProxyHostCommand command, CancellationToken ct)
    {
        var domains = command.DomainNames.ToList();

        if (domains.Count == 0)
            throw new ProxyHostValidationException("At least one domain name is required.");

        if (!Uri.TryCreate(command.DestinationUri, UriKind.Absolute, out var parsed)
            || (parsed.Scheme != "http" && parsed.Scheme != "https"))
            throw new ProxyHostValidationException(
                $"'{command.DestinationUri}' must be a valid absolute http or https URI.");

        var all = await repository.GetAllAsync(ct);
        var conflicting = all
            .SelectMany(h => h.DomainNames)
            .FirstOrDefault(d => domains.Contains(d, StringComparer.OrdinalIgnoreCase));

        if (conflicting is not null)
            throw new ProxyHostConflictException(conflicting);

        var destination = DestinationUri.Parse(command.DestinationUri);
        ProxyCertificate? cert = command.CertificatePath is not null
            ? new ProxyCertificate(command.CertificatePath, command.CertificateKeyPath)
            : null;

        var host = ProxyHost.Create(domains, destination, cert);
        await repository.AddAsync(host, ct);

        await auditLog.AppendAsync(
            AuditLogEntry.Create(command.ActorId, AuditOperation.Created, host.Id, null, null), ct);

        var dto = GetProxyHostsHandler.MapToDto(host);
        var @event = new ProxyHostCreatedEvent(
            host.Id,
            host.DomainNames.ToList(),
            host.Destination.ToString(),
            host.IsEnabled,
            DateTimeOffset.UtcNow);

        return (dto, @event);
    }
}
