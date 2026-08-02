namespace West94.ProxyManager.Files.Storage;

public interface IObjectStore
{
    Task PutAsync(
        string key,
        Stream content,
        long contentLength,
        string contentType,
        IReadOnlyDictionary<string, string>? metadata,
        CancellationToken ct);

    /// <summary>Returns <see langword="null"/> when the object does not exist.</summary>
    Task<ObjectStoreDownload?> GetAsync(string key, CancellationToken ct);

    /// <summary>Returns <see langword="null"/> when the object does not exist.</summary>
    Task<ObjectStoreStat?> StatAsync(string key, CancellationToken ct);

    /// <summary>Idempotent — deleting a key that does not exist is not an error.</summary>
    Task DeleteAsync(string key, CancellationToken ct);

    Task CopyAsync(string sourceKey, string destKey, CancellationToken ct);

    /// <summary>Pure local SigV4 signing — no network call.</summary>
    Uri CreatePresignedUrl(string key, HttpMethod method, TimeSpan ttl);
}

public sealed record ObjectStoreStat(long ContentLength, string ContentType, string ETag, DateTimeOffset LastModified);

public sealed class ObjectStoreDownload(Stream content, ObjectStoreStat stat) : IAsyncDisposable
{
    public Stream Content { get; } = content;
    public ObjectStoreStat Stat { get; } = stat;

    public async ValueTask DisposeAsync() => await Content.DisposeAsync();
}
