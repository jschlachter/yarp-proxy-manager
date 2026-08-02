using West94.ProxyManager.Files.Repositories;
using West94.ProxyManager.Files.Storage;

namespace West94.ProxyManager.Files.Services;

/// <summary>
/// The sweep logic itself, factored out of <see cref="StagedAssetSweeper"/> so it is unit-testable
/// with fakes and no DI scope or background-service host required.
/// </summary>
public static class StagedAssetSweepRunner
{
    /// <returns>The number of staged assets swept.</returns>
    public static async Task<int> SweepAsync(
        IFileAssetRepository repository, IObjectStore objectStore, TimeSpan stagingTtl, CancellationToken ct)
    {
        var cutoff = DateTimeOffset.UtcNow - stagingTtl;
        var expired = await repository.GetStagedOlderThanAsync(cutoff, ct);

        foreach (var asset in expired)
        {
            await objectStore.DeleteAsync(asset.StorageKey, ct);
            await repository.RemoveAsync(asset.Id, ct);
        }

        return expired.Count;
    }
}
