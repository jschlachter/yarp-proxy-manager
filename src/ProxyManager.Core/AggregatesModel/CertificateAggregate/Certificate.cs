using West94.ProxyManager.Core.Exceptions;
using West94.ProxyManager.Core.SeedWork;

namespace West94.ProxyManager.Core.AggregatesModel.CertificateAggregate;

public sealed class Certificate : Entity
{
    private Certificate(Guid id, string name, CertificateFormat format,
        Guid certificateAssetId, Guid? keyAssetId, string certificateFileName, string? keyFileName,
        string? passPhrase, CertificateSubjectInfo subject,
        DateTimeOffset createdAt, DateTimeOffset updatedAt)
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

    internal static Certificate Reconstitute(Guid id, string name, CertificateFormat format,
        Guid certificateAssetId, Guid? keyAssetId, string certificateFileName, string? keyFileName,
        string? passPhrase, CertificateSubjectInfo subject,
        DateTimeOffset createdAt, DateTimeOffset updatedAt) =>
        new(id, name, format, certificateAssetId, keyAssetId, certificateFileName, keyFileName,
            passPhrase, subject, createdAt, updatedAt);

    public static Certificate Create(string name, CertificateFormat format,
        Guid certificateAssetId, Guid? keyAssetId,
        string certificateFileName, string? keyFileName,
        string? passPhrase, CertificateSubjectInfo subject)
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
            certificateFileName, keyFileName, passPhrase, subject, now, now);
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
}
