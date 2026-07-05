using West94.ProxyManager.Core.AggregatesModel.CertificateAggregate;
using West94.ProxyManager.Core.DTOs;
using West94.ProxyManager.Core.Exceptions;
using West94.ProxyManager.Core.Messages.Commands;
using West94.ProxyManager.Core.Messages.Events;

namespace West94.ProxyManager.API.Handlers;

public sealed class UpdateCertificateHandler(ICertificateRepository repository)
{
    public async Task<(CertificateDto, CertificateUpdatedEvent)> Handle(UpdateCertificateCommand command, CancellationToken ct)
    {
        var cert = await repository.FindAsync(command.Id, ct)
            ?? throw new CertificateNotFoundException(command.Id);

        if (command.Name is not null)
            cert.Rename(command.Name);

        if (command.PassPhrase is not null)
            cert.UpdatePassPhrase(command.PassPhrase);

        await repository.UpdateAsync(cert, ct);

        var dto = GetCertificatesHandler.MapToDto(cert);
        var @event = new CertificateUpdatedEvent(cert.Id, cert.Name, DateTimeOffset.UtcNow);
        return (dto, @event);
    }
}
