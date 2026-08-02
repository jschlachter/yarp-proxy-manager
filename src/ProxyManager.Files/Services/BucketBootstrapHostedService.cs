using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using West94.ProxyManager.Files.Options;

namespace West94.ProxyManager.Files.Services;

/// <summary>Ensures the configured bucket exists at startup before the service begins accepting requests.</summary>
public sealed class BucketBootstrapHostedService(
    IAmazonS3 s3,
    IOptions<ObjectStorageOptions> options,
    ILogger<BucketBootstrapHostedService> logger) : IHostedService
{
    private readonly ObjectStorageOptions _options = options.Value;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.AutoCreateBucket)
        {
            return;
        }

        try
        {
            await s3.HeadBucketAsync(new HeadBucketRequest { BucketName = _options.Bucket }, cancellationToken);
            logger.LogInformation("Bucket {Bucket} already exists.", _options.Bucket);
        }
        catch (AmazonS3Exception)
        {
            logger.LogInformation("Bucket {Bucket} not found, creating it.", _options.Bucket);
            await s3.PutBucketAsync(new PutBucketRequest { BucketName = _options.Bucket }, cancellationToken);
            logger.LogInformation("Bucket {Bucket} created.", _options.Bucket);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
