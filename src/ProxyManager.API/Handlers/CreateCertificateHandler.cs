using West94.ProxyManager.Core.AggregatesModel.CertificateAggregate;
using West94.ProxyManager.Core.DTOs;
using West94.ProxyManager.Core.Exceptions;
using West94.ProxyManager.Core.Messages.Commands;
using West94.ProxyManager.Core.Messages.Events;

namespace West94.ProxyManager.API.Handlers;

public sealed class CreateCertificateHandler(ICertificateRepository repository)
{
    public async Task<(CertificateDto, CertificateCreatedEvent)> Handle(CreateCertificateCommand command, CancellationToken ct)
    {
        if (!Enum.TryParse<CertificateFormat>(command.Format, ignoreCase: true, out var format))
            throw new CertificateValidationException(
                $"'{command.Format}' is not a valid certificate format. Use 'Pfx' or 'Pem'.");

        var cert = Certificate.Create(command.Name, format, command.CertificatePath, command.KeyFilePath, command.PassPhrase);
        await repository.AddAsync(cert, ct);

        var dto = GetCertificatesHandler.MapToDto(cert);
        var @event = new CertificateCreatedEvent(cert.Id, cert.Name, cert.Format.ToString(), DateTimeOffset.UtcNow);
        return (dto, @event);
    }
}
