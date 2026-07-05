using West94.ProxyManager.Core.Exceptions;
using West94.ProxyManager.Core.SeedWork;

namespace West94.ProxyManager.Core.AggregatesModel.CertificateAggregate;

public sealed class Certificate : Entity
{
    private Certificate(Guid id, string name, CertificateFormat format,
        string certificatePath, string? keyFilePath, string? passPhrase,
        DateTimeOffset createdAt, DateTimeOffset updatedAt)
    {
        Id = id;
        Name = name;
        Format = format;
        CertificatePath = certificatePath;
        KeyFilePath = keyFilePath;
        PassPhrase = passPhrase;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public string Name { get; private set; }
    public CertificateFormat Format { get; private set; }
    public string CertificatePath { get; private set; }
    public string? KeyFilePath { get; private set; }
    public string? PassPhrase { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    internal static Certificate Reconstitute(Guid id, string name, CertificateFormat format,
        string certificatePath, string? keyFilePath, string? passPhrase,
        DateTimeOffset createdAt, DateTimeOffset updatedAt) =>
        new(id, name, format, certificatePath, keyFilePath, passPhrase, createdAt, updatedAt);

    public static Certificate Create(string name, CertificateFormat format,
        string certificatePath, string? keyFilePath = null, string? passPhrase = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new CertificateValidationException("Certificate name is required.");
        if (string.IsNullOrWhiteSpace(certificatePath))
            throw new CertificateValidationException("Certificate path is required.");
        if (format == CertificateFormat.Pfx && keyFilePath is not null)
            throw new CertificateValidationException("PFX format bundles the private key; KeyFilePath must be null.");

        var now = DateTimeOffset.UtcNow;
        return new Certificate(Guid.NewGuid(), name, format, certificatePath, keyFilePath, passPhrase, now, now);
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
