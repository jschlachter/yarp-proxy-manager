namespace West94.ProxyManager.Infrastructure.Data;

/// <summary>EF Core persistence model for ProxyHost. Decouples the ORM from the domain aggregate.</summary>
internal sealed class ProxyHostRecord
{
    public Guid Id { get; set; }
    public List<string> DomainNames { get; set; } = [];
    public string DestinationScheme { get; set; } = string.Empty;
    public string DestinationHost { get; set; } = string.Empty;
    public int DestinationPort { get; set; }
    public bool IsEnabled { get; set; }
    public string? CertificatePath { get; set; }
    public string? CertificateKeyPath { get; set; }
    public string? CertificatePassword { get; set; }
}
