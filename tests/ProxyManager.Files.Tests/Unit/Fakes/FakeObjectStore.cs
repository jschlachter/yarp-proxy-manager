using West94.ProxyManager.Files.Storage;

namespace West94.ProxyManager.Files.Tests.Unit.Fakes;

/// <summary>In-memory <see cref="IObjectStore"/> for unit tests — no S3/RustFS dependency.</summary>
public sealed class FakeObjectStore : IObjectStore
{
    private sealed record Entry(byte[] Content, string ContentType);

    private readonly Dictionary<string, Entry> _objects = [];

    public IReadOnlyDictionary<string, byte[]> Objects => _objects.ToDictionary(kv => kv.Key, kv => kv.Value.Content);

    public Task PutAsync(
        string key, Stream content, long contentLength, string contentType,
        IReadOnlyDictionary<string, string>? metadata, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        content.CopyTo(buffer);
        _objects[key] = new Entry(buffer.ToArray(), contentType);
        return Task.CompletedTask;
    }

    public Task<ObjectStoreDownload?> GetAsync(string key, CancellationToken ct)
    {
        if (!_objects.TryGetValue(key, out var entry))
        {
            return Task.FromResult<ObjectStoreDownload?>(null);
        }

        var stat = new ObjectStoreStat(entry.Content.Length, entry.ContentType, ETag: "fake-etag", DateTimeOffset.UtcNow);
        return Task.FromResult<ObjectStoreDownload?>(new ObjectStoreDownload(new MemoryStream(entry.Content), stat));
    }

    public Task<ObjectStoreStat?> StatAsync(string key, CancellationToken ct)
    {
        if (!_objects.TryGetValue(key, out var entry))
        {
            return Task.FromResult<ObjectStoreStat?>(null);
        }

        return Task.FromResult<ObjectStoreStat?>(new ObjectStoreStat(entry.Content.Length, entry.ContentType, "fake-etag", DateTimeOffset.UtcNow));
    }

    public Task DeleteAsync(string key, CancellationToken ct)
    {
        _objects.Remove(key);
        return Task.CompletedTask;
    }

    public Task CopyAsync(string sourceKey, string destKey, CancellationToken ct)
    {
        if (_objects.TryGetValue(sourceKey, out var entry))
        {
            _objects[destKey] = entry;
        }

        return Task.CompletedTask;
    }

    public Uri CreatePresignedUrl(string key, HttpMethod method, TimeSpan ttl) =>
        new($"https://fake-object-store.invalid/{key}");
}
