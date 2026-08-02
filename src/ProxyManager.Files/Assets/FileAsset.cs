namespace West94.ProxyManager.Files.Assets;

/// <summary>
/// A stored asset. Not <c>Core.SeedWork.Entity</c> — Files does not reference ProxyManager.Core
/// for its own model; it is a domain-agnostic, generic asset store.
/// </summary>
public sealed class FileAsset
{
    private FileAsset(
        Guid id, string assetType, string fileName, string contentType, long sizeBytes,
        string sha256, string storageKey, FileAssetStatus status,
        string? ownerType, Guid? ownerId, string uploadedBy,
        DateTimeOffset createdAt, DateTimeOffset? committedAt)
    {
        Id = id;
        AssetType = assetType;
        FileName = fileName;
        ContentType = contentType;
        SizeBytes = sizeBytes;
        Sha256 = sha256;
        StorageKey = storageKey;
        Status = status;
        OwnerType = ownerType;
        OwnerId = ownerId;
        UploadedBy = uploadedBy;
        CreatedAt = createdAt;
        CommittedAt = committedAt;
    }

    public Guid Id { get; private set; }
    public string AssetType { get; private set; }
    public string FileName { get; private set; }
    public string ContentType { get; private set; }
    public long SizeBytes { get; private set; }
    public string Sha256 { get; private set; }
    public string StorageKey { get; private set; }
    public FileAssetStatus Status { get; private set; }
    public string? OwnerType { get; private set; }
    public Guid? OwnerId { get; private set; }
    public string UploadedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CommittedAt { get; private set; }

    internal static FileAsset Reconstitute(
        Guid id, string assetType, string fileName, string contentType, long sizeBytes,
        string sha256, string storageKey, FileAssetStatus status,
        string? ownerType, Guid? ownerId, string uploadedBy,
        DateTimeOffset createdAt, DateTimeOffset? committedAt) =>
        new(id, assetType, fileName, contentType, sizeBytes, sha256, storageKey, status,
            ownerType, ownerId, uploadedBy, createdAt, committedAt);

    public static FileAsset CreateStaged(
        string assetType, string fileName, string contentType, long sizeBytes,
        string sha256, string storageKey, string uploadedBy)
    {
        if (string.IsNullOrWhiteSpace(assetType))
            throw new FileAssetValidationException("Asset type is required.");
        if (string.IsNullOrWhiteSpace(fileName))
            throw new FileAssetValidationException("File name is required.");
        if (string.IsNullOrWhiteSpace(storageKey))
            throw new FileAssetValidationException("Storage key is required.");
        if (sizeBytes < 0)
            throw new FileAssetValidationException("Size must not be negative.");

        return new FileAsset(
            Guid.NewGuid(), assetType, fileName, contentType, sizeBytes, sha256, storageKey,
            FileAssetStatus.Staged, ownerType: null, ownerId: null, uploadedBy, DateTimeOffset.UtcNow, committedAt: null);
    }

    /// <summary>Idempotent — committing an already-committed asset is a no-op, not an error.</summary>
    public void Commit(string ownerType, Guid ownerId, string committedStorageKey)
    {
        if (Status == FileAssetStatus.Committed)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(ownerType))
            throw new FileAssetValidationException("Owner type is required to commit an asset.");
        if (string.IsNullOrWhiteSpace(committedStorageKey))
            throw new FileAssetValidationException("Committed storage key is required.");

        StorageKey = committedStorageKey;
        Status = FileAssetStatus.Committed;
        OwnerType = ownerType;
        OwnerId = ownerId;
        CommittedAt = DateTimeOffset.UtcNow;
    }

    public void MarkDeleted() => Status = FileAssetStatus.Deleted;
}
