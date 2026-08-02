using West94.ProxyManager.Files.Assets;
using West94.ProxyManager.Files.Services;
using West94.ProxyManager.Files.Tests.Unit.Fakes;

namespace West94.ProxyManager.Files.Tests.Unit;

[Trait("Category", "Unit")]
public sealed class StagedAssetSweepRunnerTests
{
    [Fact]
    public async Task SweepAsync_RemovesExpiredOwnerlessStagedAssetsAndTheirBlobs()
    {
        var ct = TestContext.Current.CancellationToken;
        var repo = new FakeFileAssetRepository();
        var store = new FakeObjectStore();
        var expired = FileAsset.Reconstitute(
            Guid.NewGuid(), "certificate", "old.pem", "application/x-pem-file", 10, "sha",
            "staging/old/old.pem", FileAssetStatus.Staged, ownerType: null, ownerId: null,
            uploadedBy: "user", createdAt: DateTimeOffset.UtcNow.AddHours(-1), committedAt: null);
        await repo.AddAsync(expired, ct);
        await store.PutAsync(expired.StorageKey, new MemoryStream([1, 2, 3]), 3, "application/x-pem-file", null, ct);

        var swept = await StagedAssetSweepRunner.SweepAsync(repo, store, TimeSpan.FromMinutes(30), ct);

        Assert.Equal(1, swept);
        Assert.Null(await repo.FindAsync(expired.Id, ct));
        Assert.False(store.Objects.ContainsKey(expired.StorageKey));
    }

    [Fact]
    public async Task SweepAsync_LeavesRecentStagedAssetsAlone()
    {
        var ct = TestContext.Current.CancellationToken;
        var repo = new FakeFileAssetRepository();
        var store = new FakeObjectStore();
        var recent = FileAsset.CreateStaged(
            "certificate", "new.pem", "application/x-pem-file", 10, "sha", "staging/new/new.pem", "user");
        await repo.AddAsync(recent, ct);

        var swept = await StagedAssetSweepRunner.SweepAsync(repo, store, TimeSpan.FromMinutes(30), ct);

        Assert.Equal(0, swept);
        Assert.NotNull(await repo.FindAsync(recent.Id, ct));
    }

    [Fact]
    public async Task SweepAsync_LeavesCommittedAssetsAlone_EvenIfOld()
    {
        var ct = TestContext.Current.CancellationToken;
        var repo = new FakeFileAssetRepository();
        var store = new FakeObjectStore();
        var committed = FileAsset.Reconstitute(
            Guid.NewGuid(), "certificate", "old.pem", "application/x-pem-file", 10, "sha",
            "certificate/old/old.pem", FileAssetStatus.Committed, ownerType: "certificate", ownerId: Guid.NewGuid(),
            uploadedBy: "user", createdAt: DateTimeOffset.UtcNow.AddHours(-1), committedAt: DateTimeOffset.UtcNow.AddHours(-1));
        await repo.AddAsync(committed, ct);

        var swept = await StagedAssetSweepRunner.SweepAsync(repo, store, TimeSpan.FromMinutes(30), ct);

        Assert.Equal(0, swept);
        Assert.NotNull(await repo.FindAsync(committed.Id, ct));
    }
}
