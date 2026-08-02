using West94.ProxyManager.Files.Assets;
using West94.ProxyManager.Files.Contracts;
using West94.ProxyManager.Files.Storage;

namespace West94.ProxyManager.Files.Services;

public interface IFileAssetService
{
    /// <summary><paramref name="content"/> must be seekable and positioned at 0; the caller has already buffered and hashed it.</summary>
    Task<FileAsset> StageAsync(
        string assetType, string fileName, Stream content, long sizeBytes, string sha256, string uploadedBy, CancellationToken ct);

    /// <summary>Idempotent — committing an already-committed asset returns it unchanged.</summary>
    Task<FileAsset> CommitAsync(Guid id, string ownerType, Guid ownerId, CancellationToken ct);

    Task<FileAsset?> GetAsync(Guid id, CancellationToken ct);

    /// <summary>Returns <see langword="null"/> when the asset record or its blob does not exist.</summary>
    Task<ObjectStoreDownload?> GetContentAsync(Guid id, CancellationToken ct);

    Task<PagedResult<FileAssetDto>> ListAsync(string ownerType, Guid ownerId, int page, int pageSize, CancellationToken ct);

    /// <summary>Returns <see langword="false"/> when no such asset exists.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken ct);

    /// <summary>Deletes every committed asset owned by (<paramref name="ownerType"/>, <paramref name="ownerId"/>) — the event-driven cleanup path.</summary>
    Task DeleteByOwnerAsync(string ownerType, Guid ownerId, CancellationToken ct);
}
