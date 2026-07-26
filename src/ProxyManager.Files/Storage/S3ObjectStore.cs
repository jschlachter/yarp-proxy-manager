using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using West94.ProxyManager.Files.Options;

namespace West94.ProxyManager.Files.Storage;

public sealed class S3ObjectStore(IAmazonS3 s3, IOptions<ObjectStorageOptions> options) : IObjectStore
{
    private readonly ObjectStorageOptions _options = options.Value;

    public async Task PutAsync(
        string key,
        Stream content,
        long contentLength,
        string contentType,
        IReadOnlyDictionary<string, string>? metadata,
        CancellationToken ct)
    {
        var request = new PutObjectRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            InputStream = content,
            AutoCloseStream = false,
            ContentType = contentType,
        };

        if (metadata is not null)
        {
            foreach (var (metaKey, metaValue) in metadata)
            {
                request.Metadata.Add(metaKey, metaValue);
            }
        }

        await s3.PutObjectAsync(request, ct);
    }

    public async Task<ObjectStoreDownload?> GetAsync(string key, CancellationToken ct)
    {
        try
        {
            var response = await s3.GetObjectAsync(_options.Bucket, key, ct);
            var stat = new ObjectStoreStat(response.ContentLength, response.Headers.ContentType, response.ETag, ToDateTimeOffset(response.LastModified));
            return new ObjectStoreDownload(response.ResponseStream, stat);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<ObjectStoreStat?> StatAsync(string key, CancellationToken ct)
    {
        try
        {
            var response = await s3.GetObjectMetadataAsync(_options.Bucket, key, ct);
            return new ObjectStoreStat(response.ContentLength, response.Headers.ContentType, response.ETag, ToDateTimeOffset(response.LastModified));
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken ct)
    {
        try
        {
            await s3.DeleteObjectAsync(_options.Bucket, key, ct);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Idempotent — already gone.
        }
    }

    public async Task CopyAsync(string sourceKey, string destKey, CancellationToken ct)
    {
        await s3.CopyObjectAsync(new CopyObjectRequest
        {
            SourceBucket = _options.Bucket,
            SourceKey = sourceKey,
            DestinationBucket = _options.Bucket,
            DestinationKey = destKey,
        }, ct);
    }

    public Uri CreatePresignedUrl(string key, HttpMethod method, TimeSpan ttl)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            Verb = method == HttpMethod.Put ? HttpVerb.PUT : HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(ttl),
        };

        return new Uri(s3.GetPreSignedURL(request));
    }

    private static DateTimeOffset ToDateTimeOffset(DateTime? value) =>
        value is null ? default : new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Utc));
}
