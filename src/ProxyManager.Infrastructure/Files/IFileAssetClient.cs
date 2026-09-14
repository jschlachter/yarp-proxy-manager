namespace West94.ProxyManager.Infrastructure.Files;

/// <summary>Service-to-service client into ProxyManager.Files, used to fetch uploaded bytes and commit staged assets.</summary>
public interface IFileAssetClient
{
    /// <summary>Returns <see langword="null"/> when no such asset exists.</summary>
    Task<FileAssetSummary?> GetAsync(Guid id, CancellationToken ct);

    Task<byte[]> GetContentAsync(Guid id, CancellationToken ct);

    /// <summary>Idempotent on the Files side — safe to call again for an already-committed asset.</summary>
    Task CommitAsync(Guid id, string ownerType, Guid ownerId, CancellationToken ct);

    /// <summary>Stages new bytes in the Files service (multipart upload of asset type "certificate") and returns the new staged asset id.</summary>
    Task<Guid> UploadAsync(string fileName, string contentType, Stream content, CancellationToken ct);
}
