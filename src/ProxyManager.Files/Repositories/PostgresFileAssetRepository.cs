using Microsoft.EntityFrameworkCore;
using West94.ProxyManager.Files.Assets;
using West94.ProxyManager.Files.Data;

namespace West94.ProxyManager.Files.Repositories;

/// <summary>PostgreSQL-backed repository for FileAsset, scoped to the "files" schema.</summary>
public sealed class PostgresFileAssetRepository(FilesDbContext db) : IFileAssetRepository
{
    public async Task<FileAsset?> FindAsync(Guid id, CancellationToken ct = default)
    {
        var record = await db.FileAssets.FindAsync([id], ct);
        return record is null ? null : ToDomain(record);
    }

    public async Task<IReadOnlyList<FileAsset>> GetByOwnerAsync(string ownerType, Guid ownerId, CancellationToken ct = default)
    {
        var records = await db.FileAssets.AsNoTracking()
            .Where(x => x.OwnerType == ownerType && x.OwnerId == ownerId)
            .ToListAsync(ct);
        return records.ConvertAll(ToDomain);
    }

    public async Task<IReadOnlyList<FileAsset>> GetStagedOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct = default)
    {
        var records = await db.FileAssets.AsNoTracking()
            .Where(x => x.Status == (int)FileAssetStatus.Staged && x.OwnerId == null && x.CreatedAt < cutoff)
            .ToListAsync(ct);
        return records.ConvertAll(ToDomain);
    }

    public async Task AddAsync(FileAsset asset, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(asset);
        db.FileAssets.Add(ToRecord(asset));
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(FileAsset asset, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(asset);
        var existing = await db.FileAssets.FindAsync([asset.Id], ct)
            ?? throw new InvalidOperationException($"FileAsset '{asset.Id}' not found.");

        existing.StorageKey = asset.StorageKey;
        existing.Status = (int)asset.Status;
        existing.OwnerType = asset.OwnerType;
        existing.OwnerId = asset.OwnerId;
        existing.CommittedAt = asset.CommittedAt;

        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(Guid id, CancellationToken ct = default)
    {
        var record = await db.FileAssets.FindAsync([id], ct);
        if (record is null) return;

        db.FileAssets.Remove(record);
        await db.SaveChangesAsync(ct);
    }

    private static FileAsset ToDomain(FileAssetRecord r) =>
        FileAsset.Reconstitute(
            r.Id, r.AssetType, r.FileName, r.ContentType, r.SizeBytes, r.Sha256, r.StorageKey,
            (FileAssetStatus)r.Status, r.OwnerType, r.OwnerId, r.UploadedBy, r.CreatedAt, r.CommittedAt);

    private static FileAssetRecord ToRecord(FileAsset a) => new()
    {
        Id = a.Id,
        AssetType = a.AssetType,
        FileName = a.FileName,
        ContentType = a.ContentType,
        SizeBytes = a.SizeBytes,
        Sha256 = a.Sha256,
        StorageKey = a.StorageKey,
        Status = (int)a.Status,
        OwnerType = a.OwnerType,
        OwnerId = a.OwnerId,
        UploadedBy = a.UploadedBy,
        CreatedAt = a.CreatedAt,
        CommittedAt = a.CommittedAt,
    };
}
