using West94.ProxyManager.Files.Assets;
using West94.ProxyManager.Files.Contracts;
using West94.ProxyManager.Files.Repositories;
using West94.ProxyManager.Files.Storage;
using West94.ProxyManager.Files.Validation;

namespace West94.ProxyManager.Files.Services;

public sealed class FileAssetService(
    IFileAssetRepository repository,
    IObjectStore objectStore,
    UploadContentValidator validator) : IFileAssetService
{
    public async Task<FileAsset> StageAsync(
        string assetType, string fileName, Stream content, long sizeBytes, string sha256, string uploadedBy, CancellationToken ct)
    {
        var normalizedType = AssetTypeAllowlist.Normalize(assetType);
        var sanitizedName = AssetKeyBuilder.SanitizeFileName(fileName);

        var headerLength = (int)Math.Min(16, sizeBytes);
        var header = new byte[headerLength];
        if (headerLength > 0)
        {
            content.Position = 0;
            await content.ReadExactlyAsync(header, ct);
            content.Position = 0;
        }

        var contentType = validator.Validate(normalizedType, sanitizedName, header);

        // The staging key uses a fresh id, independent of the asset's own id (generated inside
        // FileAsset.CreateStaged) — all lookups go by asset id via the DB record regardless.
        var uploadId = Guid.NewGuid();
        var storageKey = AssetKeyBuilder.StagingKey(uploadId, sanitizedName);

        var asset = FileAsset.CreateStaged(normalizedType, sanitizedName, contentType, sizeBytes, sha256, storageKey, uploadedBy);

        // DB row first: if the PutAsync below throws, the row is an orphaned Staged asset with no
        // blob — exactly what the sweeper already exists to clean up. The reverse order (blob
        // first) can leave a blob with no DB row, which nothing ever sweeps.
        await repository.AddAsync(asset, ct);
        await objectStore.PutAsync(storageKey, content, sizeBytes, contentType, metadata: null, ct);

        return asset;
    }

    public async Task<FileAsset> CommitAsync(Guid id, string ownerType, Guid ownerId, CancellationToken ct)
    {
        var asset = await repository.FindAsync(id, ct) ?? throw new FileAssetNotFoundException(id);

        if (asset.Status != FileAssetStatus.Committed)
        {
            var committedKey = AssetKeyBuilder.CommittedKey(asset.AssetType, asset.Id, asset.FileName);
            await objectStore.CopyAsync(asset.StorageKey, committedKey, ct);
            await objectStore.DeleteAsync(asset.StorageKey, ct);

            asset.Commit(ownerType, ownerId, committedKey);
            await repository.UpdateAsync(asset, ct);
        }

        return asset;
    }

    public Task<FileAsset?> GetAsync(Guid id, CancellationToken ct) => repository.FindAsync(id, ct);

    public async Task<ObjectStoreDownload?> GetContentAsync(Guid id, CancellationToken ct)
    {
        var asset = await repository.FindAsync(id, ct);
        return asset is null ? null : await objectStore.GetAsync(asset.StorageKey, ct);
    }

    public async Task<PagedResult<FileAssetDto>> ListAsync(string ownerType, Guid ownerId, int page, int pageSize, CancellationToken ct)
    {
        var all = await repository.GetByOwnerAsync(ownerType, ownerId, ct);
        var items = all.Skip((page - 1) * pageSize).Take(pageSize).Select(a => a.ToDto()).ToList();
        return new PagedResult<FileAssetDto>(items, all.Count, page, pageSize);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        var asset = await repository.FindAsync(id, ct);
        if (asset is null)
        {
            return false;
        }

        await objectStore.DeleteAsync(asset.StorageKey, ct);
        asset.MarkDeleted();
        await repository.UpdateAsync(asset, ct);
        return true;
    }

    public async Task DeleteByOwnerAsync(string ownerType, Guid ownerId, CancellationToken ct)
    {
        var assets = await repository.GetByOwnerAsync(ownerType, ownerId, ct);
        foreach (var asset in assets)
        {
            await objectStore.DeleteAsync(asset.StorageKey, ct);
            asset.MarkDeleted();
            await repository.UpdateAsync(asset, ct);
        }
    }
}
