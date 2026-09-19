using West94.ProxyManager.Core.Exceptions;
using West94.ProxyManager.Core.SeedWork;

namespace West94.ProxyManager.Core.AggregatesModel.CertificateAggregate;

public sealed class Certificate : Entity
{
    private Certificate(Guid id, string name, CertificateFormat format,
        Guid certificateAssetId, Guid? keyAssetId, string certificateFileName, string? keyFileName,
        string? passPhrase, CertificateSubjectInfo subject,
        DateTimeOffset createdAt, DateTimeOffset updatedAt, CertificateSource source)
    {
        Id = id;
        Name = name;
        Format = format;
        CertificateAssetId = certificateAssetId;
        KeyAssetId = keyAssetId;
        CertificateFileName = certificateFileName;
        KeyFileName = keyFileName;
        PassPhrase = passPhrase;
        Subject = subject;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        Source = source;
    }

    public string Name { get; private set; }
    public CertificateFormat Format { get; private set; }
    public Guid CertificateAssetId { get; private set; }
    public Guid? KeyAssetId { get; private set; }
    public string CertificateFileName { get; private set; }
    public string? KeyFileName { get; private set; }
    public string? PassPhrase { get; private set; }
    public CertificateSubjectInfo Subject { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public CertificateSource Source { get; private set; }

    internal static Certificate Reconstitute(Guid id, string name, CertificateFormat format,
        Guid certificateAssetId, Guid? keyAssetId, string certificateFileName, string? keyFileName,
        string? passPhrase, CertificateSubjectInfo subject,
        DateTimeOffset createdAt, DateTimeOffset updatedAt, CertificateSource source) =>
        new(id, name, format, certificateAssetId, keyAssetId, certificateFileName, keyFileName,
            passPhrase, subject, createdAt, updatedAt, source);

    public static Certificate Create(string name, CertificateFormat format,
        Guid certificateAssetId, Guid? keyAssetId,
        string certificateFileName, string? keyFileName,
        string? passPhrase, CertificateSubjectInfo subject,
        CertificateSource source = CertificateSource.Manual)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new CertificateValidationException("Certificate name is required.");
        if (certificateAssetId == Guid.Empty)
            throw new CertificateValidationException("Certificate asset id is required.");
        if (format == CertificateFormat.Pfx && keyAssetId is not null)
            throw new CertificateValidationException("PFX bundles the private key; KeyAssetId must be null.");
        ArgumentNullException.ThrowIfNull(subject);

        var now = DateTimeOffset.UtcNow;
        return new Certificate(Guid.NewGuid(), name, format, certificateAssetId, keyAssetId,
            certificateFileName, keyFileName, passPhrase, subject, now, now, source);
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new CertificateValidationException("Certificate name is required.");
        Name = name;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdatePassPhrase(string? passPhrase)
    {
        PassPhrase = passPhrase;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Replaces the certificate's issued assets in place (e.g. after a Let's Encrypt renewal), preserving <see cref="Entity.Id"/>.</summary>
    public void ReplaceAssets(Guid certificateAssetId, Guid? keyAssetId, string certificateFileName, string? keyFileName, CertificateSubjectInfo subject)
    {
        if (certificateAssetId == Guid.Empty)
            throw new CertificateValidationException("Certificate asset id is required.");
        if (Format == CertificateFormat.Pfx && keyAssetId is not null)
            throw new CertificateValidationException("PFX bundles the private key; KeyAssetId must be null.");
        ArgumentNullException.ThrowIfNull(subject);

        CertificateAssetId = certificateAssetId;
        KeyAssetId = keyAssetId;
        CertificateFileName = certificateFileName;
        KeyFileName = keyFileName;
        Subject = subject;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
