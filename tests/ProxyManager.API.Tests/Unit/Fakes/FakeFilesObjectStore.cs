extern alias ProxyManagerFilesApp;

using ProxyManagerFilesApp::West94.ProxyManager.Files.Storage;

namespace West94.ProxyManager.API.Tests.Unit.Fakes;

/// <summary>In-memory <see cref="IObjectStore"/> substituted into <see cref="Helpers.TestFilesAppFactory"/> so
/// Files-service integration tests don't need a live S3/RustFS instance.</summary>
public sealed class FakeFilesObjectStore : IObjectStore
{
    private sealed record Entry(byte[] Content, string ContentType);

    private readonly Dictionary<string, Entry> _objects = [];

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
