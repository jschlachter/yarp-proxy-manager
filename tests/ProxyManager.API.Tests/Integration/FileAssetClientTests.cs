using System.Text;
using West94.ProxyManager.API.Tests.Helpers;
using West94.ProxyManager.Infrastructure.Files;

namespace West94.ProxyManager.API.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class FileAssetClientTests : IAsyncLifetime
{
    private TestFilesAppFactory _factory = null!;
    private IFileAssetClient _client = null!;

    ValueTask IAsyncLifetime.InitializeAsync()
    {
        _factory = new TestFilesAppFactory();
        var httpClient = _factory.CreateClient();
        httpClient.DefaultRequestHeaders.Add("X-Files-Service-Token", TestFilesAppFactory.ServiceToken);
        _client = new FileAssetClient(httpClient);
        return ValueTask.CompletedTask;
    }

    async ValueTask IAsyncDisposable.DisposeAsync() => await _factory.DisposeAsync();

    [Fact]
    public async Task UploadAsync_StagesBytes_ThatGetContentAsyncCanReadBack()
    {
        var content = Encoding.UTF8.GetBytes("-----BEGIN CERTIFICATE-----\nfake\n-----END CERTIFICATE-----");

        Guid assetId;
        using (var stream = new MemoryStream(content))
        {
            assetId = await _client.UploadAsync("cert.pem", "application/x-pem-file", stream, CancellationToken.None);
        }

        Assert.NotEqual(Guid.Empty, assetId);

        var summary = await _client.GetAsync(assetId, CancellationToken.None);
        Assert.NotNull(summary);
        Assert.Equal("cert.pem", summary.FileName);
        Assert.Equal("Staged", summary.Status);

        var readBack = await _client.GetContentAsync(assetId, CancellationToken.None);
        Assert.Equal(content, readBack);
    }

    [Fact]
    public async Task UploadAsync_ThenCommitAsync_MarksAssetCommitted()
    {
        var content = Encoding.UTF8.GetBytes("fake-cert-bytes");
        Guid assetId;
        using (var stream = new MemoryStream(content))
        {
            assetId = await _client.UploadAsync("commit-me.pem", "application/x-pem-file", stream, CancellationToken.None);
        }

        await _client.CommitAsync(assetId, "certificate", Guid.NewGuid(), CancellationToken.None);

        var summary = await _client.GetAsync(assetId, CancellationToken.None);
        Assert.NotNull(summary);
        Assert.Equal("Committed", summary.Status);
    }
}
