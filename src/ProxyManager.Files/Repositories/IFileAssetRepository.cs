using West94.ProxyManager.Files.Assets;

namespace West94.ProxyManager.Files.Repositories;

public interface IFileAssetRepository
{
    Task<FileAsset?> FindAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<FileAsset>> GetByOwnerAsync(string ownerType, Guid ownerId, CancellationToken ct = default);

    /// <summary>Staged assets older than <paramref name="cutoff"/> with no owner yet assigned — sweeper eligibility.</summary>
    Task<IReadOnlyList<FileAsset>> GetStagedOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct = default);

    Task AddAsync(FileAsset asset, CancellationToken ct = default);

    Task UpdateAsync(FileAsset asset, CancellationToken ct = default);

    Task RemoveAsync(Guid id, CancellationToken ct = default);
}
