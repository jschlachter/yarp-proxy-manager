using West94.ProxyManager.Core.AggregatesModel.CertificateAggregate;
using West94.ProxyManager.Core.AggregatesModel.ProxyHostAggregate;
using West94.ProxyManager.Core.DTOs;
using West94.ProxyManager.Core.Exceptions;
using West94.ProxyManager.Core.Messages.Commands;
using West94.ProxyManager.Core.Messages.Events;

namespace West94.ProxyManager.API.Handlers;

public sealed class AssignCertificateHandler(IProxyHostRepository hostRepository, ICertificateRepository certRepository)
{
    public async Task<(ProxyHostDto, ProxyHostUpdatedEvent)> Handle(AssignCertificateCommand command, CancellationToken ct)
    {
        var host = await hostRepository.FindAsync(command.ProxyHostId, ct)
            ?? throw new ProxyHostNotFoundException(command.ProxyHostId);

        if (command.CertificateId is not null)
        {
            _ = await certRepository.FindAsync(command.CertificateId.Value, ct)
                ?? throw new CertificateNotFoundException(command.CertificateId.Value);
        }

        host.AssignCertificate(command.CertificateId);
        await hostRepository.UpdateAsync(host, ct);

        var dto = GetProxyHostsHandler.MapToDto(host);
        var @event = new ProxyHostUpdatedEvent(
            host.Id,
            host.DomainNames.ToList(),
            host.Destination.ToString(),
            host.IsEnabled,
            DateTimeOffset.UtcNow);

        return (dto, @event);
    }
}
