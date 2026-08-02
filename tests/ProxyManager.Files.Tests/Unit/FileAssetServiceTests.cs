using West94.ProxyManager.Files.Assets;
using West94.ProxyManager.Files.Options;
using West94.ProxyManager.Files.Services;
using West94.ProxyManager.Files.Tests.Unit.Fakes;
using West94.ProxyManager.Files.Validation;

namespace West94.ProxyManager.Files.Tests.Unit;

[Trait("Category", "Unit")]
public sealed class FileAssetServiceTests
{
    private static readonly byte[] PemBytes = "-----BEGIN CERTIFICATE-----\nMIIBfake\n-----END CERTIFICATE-----\n"u8.ToArray();

    private static FileAssetService CreateService(FakeFileAssetRepository repo, FakeObjectStore store) =>
        new(repo, store, new UploadContentValidator(Microsoft.Extensions.Options.Options.Create(new UploadOptions())));

    [Fact]
    public async Task StageAsync_CreatesStagedAssetAndStoresBlob()
    {
        var repo = new FakeFileAssetRepository();
        var store = new FakeObjectStore();
        var service = CreateService(repo, store);
        var ct = TestContext.Current.CancellationToken;

        using var content = new MemoryStream(PemBytes);
        var asset = await service.StageAsync("certificate", "cert.pem", content, PemBytes.Length, "sha", "user@example.com", ct);

        Assert.Equal(FileAssetStatus.Staged, asset.Status);
        Assert.Equal("application/x-pem-file", asset.ContentType);
        Assert.Equal(asset, await repo.FindAsync(asset.Id, ct));
        Assert.True(store.Objects.ContainsKey(asset.StorageKey));
    }

    [Fact]
    public async Task StageAsync_Throws_ForUnsupportedContent()
    {
        var service = CreateService(new FakeFileAssetRepository(), new FakeObjectStore());
        var ct = TestContext.Current.CancellationToken;
        using var content = new MemoryStream("not a certificate"u8.ToArray());

        await Assert.ThrowsAsync<UnsupportedAssetContentException>(() =>
            service.StageAsync("certificate", "cert.pem", content, 17, "sha", "user@example.com", ct));
    }

    [Fact]
    public async Task CommitAsync_MovesBlobToCommittedKeyAndSetsOwner()
    {
        var repo = new FakeFileAssetRepository();
        var store = new FakeObjectStore();
        var service = CreateService(repo, store);
        var ct = TestContext.Current.CancellationToken;
        using var content = new MemoryStream(PemBytes);
        var staged = await service.StageAsync("certificate", "cert.pem", content, PemBytes.Length, "sha", "user@example.com", ct);
        var stagingKey = staged.StorageKey;
        var ownerId = Guid.NewGuid();

        var committed = await service.CommitAsync(staged.Id, "certificate", ownerId, ct);

        Assert.Equal(FileAssetStatus.Committed, committed.Status);
        Assert.Equal(ownerId, committed.OwnerId);
        Assert.False(store.Objects.ContainsKey(stagingKey));
        Assert.True(store.Objects.ContainsKey(committed.StorageKey));
    }

    [Fact]
    public async Task CommitAsync_IsIdempotent_SecondCallIsANoOp()
    {
        var repo = new FakeFileAssetRepository();
        var store = new FakeObjectStore();
        var service = CreateService(repo, store);
        var ct = TestContext.Current.CancellationToken;
        using var content = new MemoryStream(PemBytes);
        var staged = await service.StageAsync("certificate", "cert.pem", content, PemBytes.Length, "sha", "user@example.com", ct);
        var ownerId = Guid.NewGuid();
        var first = await service.CommitAsync(staged.Id, "certificate", ownerId, ct);

        var second = await service.CommitAsync(staged.Id, "certificate", Guid.NewGuid(), ct);

        Assert.Equal(first.StorageKey, second.StorageKey);
        Assert.Equal(ownerId, second.OwnerId);
    }

    [Fact]
    public async Task CommitAsync_Throws_WhenAssetNotFound()
    {
        var service = CreateService(new FakeFileAssetRepository(), new FakeObjectStore());
        var ct = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<FileAssetNotFoundException>(() =>
            service.CommitAsync(Guid.NewGuid(), "certificate", Guid.NewGuid(), ct));
    }

    [Fact]
    public async Task GetContentAsync_ReturnsNull_WhenAssetNotFound()
    {
        var service = CreateService(new FakeFileAssetRepository(), new FakeObjectStore());
        var ct = TestContext.Current.CancellationToken;

        var download = await service.GetContentAsync(Guid.NewGuid(), ct);

        Assert.Null(download);
    }

    [Fact]
    public async Task ListAsync_PaginatesAssetsByOwner()
    {
        var repo = new FakeFileAssetRepository();
        var store = new FakeObjectStore();
        var service = CreateService(repo, store);
        var ct = TestContext.Current.CancellationToken;
        var ownerId = Guid.NewGuid();

        for (var i = 0; i < 3; i++)
        {
            using var content = new MemoryStream(PemBytes);
            var staged = await service.StageAsync("certificate", $"cert{i}.pem", content, PemBytes.Length, "sha", "user", ct);
            await service.CommitAsync(staged.Id, "certificate", ownerId, ct);
        }

        var page = await service.ListAsync("certificate", ownerId, page: 1, pageSize: 2, ct);

        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, page.Items.Count);
    }

    [Fact]
    public async Task DeleteAsync_RemovesBlobAndMarksDeleted()
    {
        var repo = new FakeFileAssetRepository();
        var store = new FakeObjectStore();
        var service = CreateService(repo, store);
        var ct = TestContext.Current.CancellationToken;
        using var content = new MemoryStream(PemBytes);
        var staged = await service.StageAsync("certificate", "cert.pem", content, PemBytes.Length, "sha", "user", ct);

        var deleted = await service.DeleteAsync(staged.Id, ct);

        Assert.True(deleted);
        Assert.False(store.Objects.ContainsKey(staged.StorageKey));
        var reloaded = await repo.FindAsync(staged.Id, ct);
        Assert.Equal(FileAssetStatus.Deleted, reloaded!.Status);
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenAssetNotFound()
    {
        var service = CreateService(new FakeFileAssetRepository(), new FakeObjectStore());
        var ct = TestContext.Current.CancellationToken;

        var deleted = await service.DeleteAsync(Guid.NewGuid(), ct);

        Assert.False(deleted);
    }
}
