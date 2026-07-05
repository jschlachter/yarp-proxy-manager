namespace West94.ProxyManager.Infrastructure.Data;

/// <summary>EF Core persistence model for Certificate. Decouples the ORM from the domain aggregate.</summary>
internal sealed class CertificateRecord
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Format { get; set; }
    public string CertificatePath { get; set; } = string.Empty;
    public string? KeyFilePath { get; set; }
    public string? PassPhrase { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
