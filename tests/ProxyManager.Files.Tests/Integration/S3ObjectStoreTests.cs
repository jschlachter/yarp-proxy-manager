using System.Text;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Options;
using West94.ProxyManager.Files.Options;
using West94.ProxyManager.Files.Storage;

namespace West94.ProxyManager.Files.Tests.Integration;

/// <summary>
/// Phase 0 acceptance gate: proves AWSSDK.S3 + RustFS actually round-trip, including the v4
/// checksum-header workaround, against a real, already-running RustFS instance.
/// Requires RUSTFS_ACCESS_KEY / RUSTFS_SECRET_KEY env vars and RustFS reachable at RUSTFS_SERVICE_URL
/// (defaults to http://localhost:9000).
/// </summary>
[Trait("Category", "Integration")]
public sealed class S3ObjectStoreTests : IAsyncLifetime
{
    private const string Bucket = "proxymanager";

    private IAmazonS3 _s3 = null!;
    private S3ObjectStore _store = null!;
    private string _key = null!;

    public ValueTask InitializeAsync()
    {
        var accessKey = Environment.GetEnvironmentVariable("RUSTFS_ACCESS_KEY")
            ?? throw new InvalidOperationException("RUSTFS_ACCESS_KEY must be set to run this integration test.");
        var secretKey = Environment.GetEnvironmentVariable("RUSTFS_SECRET_KEY")
            ?? throw new InvalidOperationException("RUSTFS_SECRET_KEY must be set to run this integration test.");
        var serviceUrl = Environment.GetEnvironmentVariable("RUSTFS_SERVICE_URL") ?? "http://localhost:9000";

        var options = new ObjectStorageOptions
        {
            ServiceUrl = serviceUrl,
            Bucket = Bucket,
            Region = "us-east-1",
            AccessKey = accessKey,
            SecretKey = secretKey,
            ForcePathStyle = true,
        };

        var config = new AmazonS3Config
        {
            ServiceURL = options.ServiceUrl,
            ForcePathStyle = options.ForcePathStyle,
            AuthenticationRegion = options.Region,
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        };

        _s3 = new AmazonS3Client(new BasicAWSCredentials(options.AccessKey, options.SecretKey), config);
        _store = new S3ObjectStore(_s3, Microsoft.Extensions.Options.Options.Create(options));
        _key = $"staging/spike-test/{Guid.NewGuid():N}.txt";

        return new ValueTask(EnsureBucketAsync());
    }

    // BucketBootstrapHostedService normally does this at host startup; the test host isn't running here.
    private async Task EnsureBucketAsync()
    {
        try
        {
            await _s3.HeadBucketAsync(new Amazon.S3.Model.HeadBucketRequest { BucketName = Bucket });
        }
        catch (AmazonS3Exception)
        {
            await _s3.PutBucketAsync(new Amazon.S3.Model.PutBucketRequest { BucketName = Bucket });
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _store.DeleteAsync(_key, CancellationToken.None);
        _s3.Dispose();
    }

    [Fact]
    public async Task PutGetStatPresignDelete_RoundTrips_AgainstRealRustFs()
    {
        var ct = TestContext.Current.CancellationToken;
        var payload = "phase-0 spike"u8.ToArray();
        using (var content = new MemoryStream(payload))
        {
            await _store.PutAsync(_key, content, payload.Length, "text/plain", metadata: null, ct);
        }

        var stat = await _store.StatAsync(_key, ct);
        Assert.NotNull(stat);
        Assert.Equal(payload.Length, stat!.ContentLength);

        await using (var download = await _store.GetAsync(_key, ct))
        {
            Assert.NotNull(download);
            using var reader = new StreamReader(download!.Content, Encoding.UTF8);
            var body = await reader.ReadToEndAsync(ct);
            Assert.Equal("phase-0 spike", body);
        }

        var presigned = _store.CreatePresignedUrl(_key, HttpMethod.Get, TimeSpan.FromMinutes(5));
        Assert.Contains(_key, Uri.UnescapeDataString(presigned.ToString()));

        await _store.DeleteAsync(_key, ct);
        var afterDelete = await _store.StatAsync(_key, ct);
        Assert.Null(afterDelete);

        // Idempotent delete of an already-missing key must not throw.
        await _store.DeleteAsync(_key, ct);
    }
}
