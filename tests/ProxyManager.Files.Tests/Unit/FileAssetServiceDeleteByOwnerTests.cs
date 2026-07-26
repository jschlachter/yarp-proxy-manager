using West94.ProxyManager.Files.Assets;
using West94.ProxyManager.Files.Options;
using West94.ProxyManager.Files.Services;
using West94.ProxyManager.Files.Tests.Unit.Fakes;
using West94.ProxyManager.Files.Validation;

namespace West94.ProxyManager.Files.Tests.Unit;

public sealed class FileAssetServiceDeleteByOwnerTests
{
    private static FileAssetService CreateService(FakeFileAssetRepository repo, FakeObjectStore store) =>
        new(repo, store, new UploadContentValidator(Microsoft.Extensions.Options.Options.Create(new UploadOptions())));

    [Fact]
    public async Task DeleteByOwnerAsync_DeletesAllCommittedAssetsForOwner_AndTheirBlobs()
    {
        var repo = new FakeFileAssetRepository();
        var store = new FakeObjectStore();
        var service = CreateService(repo, store);
        var ct = TestContext.Current.CancellationToken;
        var ownerId = Guid.NewGuid();

        var pemBytes = "-----BEGIN CERTIFICATE-----\nfake\n-----END CERTIFICATE-----\n"u8.ToArray();
        var assetIds = new List<Guid>();
        for (var i = 0; i < 2; i++)
        {
            using var content = new MemoryStream(pemBytes);
            var staged = await service.StageAsync("certificate", $"cert{i}.pem", content, pemBytes.Length, "sha", "user", ct);
            await service.CommitAsync(staged.Id, "certificate", ownerId, ct);
            assetIds.Add(staged.Id);
        }

        await service.DeleteByOwnerAsync("certificate", ownerId, ct);

        foreach (var id in assetIds)
        {
            var reloaded = await repo.FindAsync(id, ct);
            Assert.Equal(FileAssetStatus.Deleted, reloaded!.Status);
            Assert.False(store.Objects.ContainsKey(reloaded.StorageKey));
        }
    }

    [Fact]
    public async Task DeleteByOwnerAsync_NoAssetsForOwner_DoesNotThrow()
    {
        var repo = new FakeFileAssetRepository();
        var store = new FakeObjectStore();
        var service = CreateService(repo, store);
        var ct = TestContext.Current.CancellationToken;

        await service.DeleteByOwnerAsync("certificate", Guid.NewGuid(), ct);
    }
}
