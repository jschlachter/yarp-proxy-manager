using West94.ProxyManager.Core.AggregatesModel.CertificateAggregate;
using West94.ProxyManager.Core.Exceptions;
using West94.ProxyManager.Core.Messages.Commands;
using West94.ProxyManager.Core.Messages.Events;

namespace West94.ProxyManager.API.Handlers;

public sealed class DeleteCertificateHandler(ICertificateRepository repository)
{
    public async Task<CertificateDeletedEvent> Handle(DeleteCertificateCommand command, CancellationToken ct)
    {
        var cert = await repository.FindAsync(command.Id, ct)
            ?? throw new CertificateNotFoundException(command.Id);

        await repository.RemoveAsync(cert.Id, ct);

        // Blob cleanup is event-driven: Files subscribes to CertificateDeletedEvent and deletes by
        // owner. This handler no longer needs Files credentials or liveness to delete a certificate.
        return new CertificateDeletedEvent(cert.Id, DateTimeOffset.UtcNow);
    }
}
