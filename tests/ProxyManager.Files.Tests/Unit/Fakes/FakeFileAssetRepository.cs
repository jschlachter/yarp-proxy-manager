using West94.ProxyManager.Files.Assets;
using West94.ProxyManager.Files.Repositories;

namespace West94.ProxyManager.Files.Tests.Unit.Fakes;

/// <summary>In-memory <see cref="IFileAssetRepository"/> for unit tests — no Postgres dependency.</summary>
public sealed class FakeFileAssetRepository : IFileAssetRepository
{
    private readonly Dictionary<Guid, FileAsset> _assets = [];

    public Task<FileAsset?> FindAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_assets.TryGetValue(id, out var asset) ? asset : null);

    public Task<IReadOnlyList<FileAsset>> GetByOwnerAsync(string ownerType, Guid ownerId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<FileAsset>>(
            _assets.Values.Where(a => a.OwnerType == ownerType && a.OwnerId == ownerId).ToList());

    public Task<IReadOnlyList<FileAsset>> GetStagedOlderThanAsync(DateTimeOffset cutoff, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<FileAsset>>(
            _assets.Values.Where(a => a.Status == FileAssetStatus.Staged && a.OwnerId is null && a.CreatedAt < cutoff).ToList());

    public Task AddAsync(FileAsset asset, CancellationToken ct = default)
    {
        _assets[asset.Id] = asset;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(FileAsset asset, CancellationToken ct = default)
    {
        _assets[asset.Id] = asset;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(Guid id, CancellationToken ct = default)
    {
        _assets.Remove(id);
        return Task.CompletedTask;
    }
}
