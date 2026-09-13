using West94.ProxyManager.Infrastructure.Files;

namespace West94.ProxyManager.API.Tests.Unit.Fakes;

/// <summary>In-memory <see cref="IFileAssetClient"/> for unit tests — no Files service dependency.</summary>
public sealed class FakeFileAssetClient : IFileAssetClient
{
    private readonly Dictionary<Guid, (FileAssetSummary Summary, byte[] Content)> _assets = [];
    public List<(Guid Id, string OwnerType, Guid OwnerId)> Commits { get; } = [];

    public void Seed(Guid id, string fileName, byte[] content, string status = "Staged") =>
        _assets[id] = (new FileAssetSummary(id, fileName, status), content);

    public Task<FileAssetSummary?> GetAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_assets.TryGetValue(id, out var entry) ? entry.Summary : null);

    public Task<byte[]> GetContentAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_assets.TryGetValue(id, out var entry) ? entry.Content : throw new InvalidOperationException("Asset not seeded."));

    public Task CommitAsync(Guid id, string ownerType, Guid ownerId, CancellationToken ct)
    {
        Commits.Add((id, ownerType, ownerId));
        if (_assets.TryGetValue(id, out var entry))
        {
            _assets[id] = (entry.Summary with { Status = "Committed" }, entry.Content);
        }
        return Task.CompletedTask;
    }

    public List<(string FileName, string ContentType, byte[] Content)> Uploads { get; } = [];

    public Task<Guid> UploadAsync(string fileName, string contentType, Stream content, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        content.CopyTo(buffer);
        var bytes = buffer.ToArray();

        var id = Guid.NewGuid();
        Uploads.Add((fileName, contentType, bytes));
        _assets[id] = (new FileAssetSummary(id, fileName, "Staged"), bytes);
        return Task.FromResult(id);
    }
}
