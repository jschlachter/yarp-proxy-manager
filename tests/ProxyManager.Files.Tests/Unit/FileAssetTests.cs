using West94.ProxyManager.Files.Assets;

namespace West94.ProxyManager.Files.Tests.Unit;

public sealed class FileAssetTests
{
    [Fact]
    public void CreateStaged_SetsStatusStagedWithNoOwner()
    {
        var asset = FileAsset.CreateStaged(
            "certificate", "cert.pem", "application/x-pem-file", 1024,
            sha256: "abc", storageKey: "staging/x/cert.pem", uploadedBy: "user@example.com");

        Assert.Equal(FileAssetStatus.Staged, asset.Status);
        Assert.Null(asset.OwnerType);
        Assert.Null(asset.OwnerId);
        Assert.Null(asset.CommittedAt);
    }

    [Fact]
    public void Commit_SetsOwnerAndCommittedKey()
    {
        var asset = FileAsset.CreateStaged(
            "certificate", "cert.pem", "application/x-pem-file", 1024,
            sha256: "abc", storageKey: "staging/x/cert.pem", uploadedBy: "user@example.com");
        var ownerId = Guid.NewGuid();

        asset.Commit("certificate", ownerId, "certificate/y/cert.pem");

        Assert.Equal(FileAssetStatus.Committed, asset.Status);
        Assert.Equal("certificate", asset.OwnerType);
        Assert.Equal(ownerId, asset.OwnerId);
        Assert.Equal("certificate/y/cert.pem", asset.StorageKey);
        Assert.NotNull(asset.CommittedAt);
    }

    [Fact]
    public void Commit_IsIdempotent_WhenAlreadyCommitted()
    {
        var asset = FileAsset.CreateStaged(
            "certificate", "cert.pem", "application/x-pem-file", 1024,
            sha256: "abc", storageKey: "staging/x/cert.pem", uploadedBy: "user@example.com");
        var ownerId = Guid.NewGuid();
        asset.Commit("certificate", ownerId, "certificate/y/cert.pem");
        var committedAt = asset.CommittedAt;

        // A second commit with different args must not change state — idempotent per plan.
        asset.Commit("certificate", Guid.NewGuid(), "certificate/z/cert.pem");

        Assert.Equal(ownerId, asset.OwnerId);
        Assert.Equal("certificate/y/cert.pem", asset.StorageKey);
        Assert.Equal(committedAt, asset.CommittedAt);
    }

    [Fact]
    public void CreateStaged_ThrowsForMissingAssetType()
    {
        Assert.Throws<FileAssetValidationException>(() =>
            FileAsset.CreateStaged("", "cert.pem", "application/x-pem-file", 1024, "abc", "key", "user"));
    }

    [Fact]
    public void CreateStaged_ThrowsForNegativeSize()
    {
        Assert.Throws<FileAssetValidationException>(() =>
            FileAsset.CreateStaged("certificate", "cert.pem", "application/x-pem-file", -1, "abc", "key", "user"));
    }
}
