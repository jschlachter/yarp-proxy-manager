using West94.ProxyManager.Core.Messages.Events;
using West94.ProxyManager.Files.Integrations;
using West94.ProxyManager.Files.Services;
using West94.ProxyManager.Files.Storage;
using West94.ProxyManager.Files.Tests.Unit.Fakes;

namespace West94.ProxyManager.Files.Tests.Unit;

/// <summary>
/// Wolverine's message dispatch itself isn't exercised here (that needs a broker/host) — this
/// verifies the handler translates the event into the right service call, which is the part
/// that's actually ours to get right.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CertificateAssetCleanupHandlerTests
{
    private sealed class SpyFileAssetService : IFileAssetService
    {
        public (string OwnerType, Guid OwnerId)? DeletedOwner { get; private set; }

        public Task DeleteByOwnerAsync(string ownerType, Guid ownerId, CancellationToken ct)
        {
            DeletedOwner = (ownerType, ownerId);
            return Task.CompletedTask;
        }

        public Task<West94.ProxyManager.Files.Assets.FileAsset> StageAsync(string assetType, string fileName, Stream content, long sizeBytes, string sha256, string uploadedBy, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<West94.ProxyManager.Files.Assets.FileAsset> CommitAsync(Guid id, string ownerType, Guid ownerId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<West94.ProxyManager.Files.Assets.FileAsset?> GetAsync(Guid id, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<ObjectStoreDownload?> GetContentAsync(Guid id, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<West94.ProxyManager.Files.Contracts.PagedResult<West94.ProxyManager.Files.Contracts.FileAssetDto>> ListAsync(string ownerType, Guid ownerId, int page, int pageSize, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<bool> DeleteAsync(Guid id, CancellationToken ct) =>
            throw new NotSupportedException();
    }

    [Fact]
    public async Task Handle_DelegatesToDeleteByOwnerAsync_WithCertificateOwnerType()
    {
        var spy = new SpyFileAssetService();
        var handler = new CertificateAssetCleanupHandler();
        var certificateId = Guid.NewGuid();
        var ct = TestContext.Current.CancellationToken;

        await handler.Handle(new CertificateDeletedEvent(certificateId, DateTimeOffset.UtcNow), spy, ct);

        Assert.Equal(("certificate", certificateId), spy.DeletedOwner);
    }
}
