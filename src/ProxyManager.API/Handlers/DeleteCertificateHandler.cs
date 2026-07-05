using Microsoft.Extensions.Logging;
using West94.ProxyManager.Core.AggregatesModel.CertificateAggregate;
using West94.ProxyManager.Core.Exceptions;
using West94.ProxyManager.Core.Messages.Commands;
using West94.ProxyManager.Core.Messages.Events;

namespace West94.ProxyManager.API.Handlers;

public sealed class DeleteCertificateHandler(ICertificateRepository repository, ILogger<DeleteCertificateHandler> logger)
{
    public async Task<CertificateDeletedEvent> Handle(DeleteCertificateCommand command, CancellationToken ct)
    {
        var cert = await repository.FindAsync(command.Id, ct)
            ?? throw new CertificateNotFoundException(command.Id);

        await repository.RemoveAsync(cert.Id, ct);

        TryDeleteFile(cert.CertificatePath);
        if (cert.KeyFilePath is not null)
            TryDeleteFile(cert.KeyFilePath);

        return new CertificateDeletedEvent(cert.Id, DateTimeOffset.UtcNow);
    }

    private void TryDeleteFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (Exception ex) { logger.LogWarning(ex, "Failed to delete certificate file {Path}", path); }
    }
}
