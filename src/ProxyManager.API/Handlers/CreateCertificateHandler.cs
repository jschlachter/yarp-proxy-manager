using West94.ProxyManager.API.Infrastructure.Files;
using West94.ProxyManager.Core.AggregatesModel.CertificateAggregate;
using West94.ProxyManager.Core.Certificates;
using West94.ProxyManager.Core.DTOs;
using West94.ProxyManager.Core.Exceptions;
using West94.ProxyManager.Core.Messages.Commands;
using West94.ProxyManager.Core.Messages.Events;

namespace West94.ProxyManager.API.Handlers;

public sealed class CreateCertificateHandler(
    ICertificateRepository repository, IFileAssetClient files, ILogger<CreateCertificateHandler> logger)
{
    public async Task<(CertificateDto, CertificateCreatedEvent)> Handle(CreateCertificateCommand command, CancellationToken ct)
    {
        if (!Enum.TryParse<CertificateFormat>(command.Format, ignoreCase: true, out var format))
            throw new CertificateValidationException(
                $"'{command.Format}' is not a valid certificate format. Use 'Pfx' or 'Pem'.");
        if (format == CertificateFormat.Pfx && command.KeyAssetId is not null)
            throw new CertificateValidationException("PFX bundles the private key; KeyAssetId must be null.");

        var certAsset = await files.GetAsync(command.CertificateAssetId, ct)
            ?? throw new CertificateValidationException($"No file asset with id '{command.CertificateAssetId}' was found.");
        var certBytes = await files.GetContentAsync(command.CertificateAssetId, ct);

        FileAssetSummary? keyAsset = null;
        var keyBytes = ReadOnlyMemory<byte>.Empty;
        if (command.KeyAssetId is { } keyAssetId)
        {
            keyAsset = await files.GetAsync(keyAssetId, ct)
                ?? throw new CertificateValidationException($"No file asset with id '{keyAssetId}' was found.");
            keyBytes = await files.GetContentAsync(keyAssetId, ct);
        }

        var subject = X509CertificateInspector.Inspect(certBytes, keyBytes.Span, format, command.PassPhrase);
        if (subject.NotAfter < DateTimeOffset.UtcNow)
        {
            logger.LogWarning(
                "Certificate '{Name}' is already expired (NotAfter: {NotAfter}) — allowed, uploading a soon-to-be-renewed certificate is legitimate.",
                command.Name, subject.NotAfter);
        }

        var cert = Certificate.Create(
            command.Name, format, command.CertificateAssetId, command.KeyAssetId,
            certAsset.FileName, keyAsset?.FileName, command.PassPhrase, subject);

        await repository.AddAsync(cert, ct);

        await files.CommitAsync(cert.CertificateAssetId, "certificate", cert.Id, ct);
        if (cert.KeyAssetId is { } committedKeyAssetId)
        {
            await files.CommitAsync(committedKeyAssetId, "certificate", cert.Id, ct);
        }

        var dto = GetCertificatesHandler.MapToDto(cert);
        var @event = new CertificateCreatedEvent(cert.Id, cert.Name, cert.Format.ToString(), DateTimeOffset.UtcNow);
        return (dto, @event);
    }
}
