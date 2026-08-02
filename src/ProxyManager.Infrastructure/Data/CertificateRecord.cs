namespace West94.ProxyManager.Infrastructure.Data;

/// <summary>EF Core persistence model for Certificate. Decouples the ORM from the domain aggregate.</summary>
internal sealed class CertificateRecord
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Format { get; set; }
    public Guid CertificateAssetId { get; set; }
    public Guid? KeyAssetId { get; set; }
    public string CertificateFileName { get; set; } = string.Empty;
    public string? KeyFileName { get; set; }
    public string? PassPhrase { get; set; }
    public string Subject { get; set; } = string.Empty;
    public List<string> SubjectAlternativeNames { get; set; } = [];
    public DateTimeOffset NotBefore { get; set; }
    public DateTimeOffset NotAfter { get; set; }
    public string Thumbprint { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
