namespace West94.ProxyManager.Files.Data;

/// <summary>EF Core persistence model for FileAsset. Decouples the ORM from the domain type.</summary>
internal sealed class FileAssetRecord
{
    public Guid Id { get; set; }
    public string AssetType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public int Status { get; set; }
    public string? OwnerType { get; set; }
    public Guid? OwnerId { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CommittedAt { get; set; }
}
