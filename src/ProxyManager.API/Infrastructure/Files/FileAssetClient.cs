using System.Net;
using System.Text.Json;

namespace West94.ProxyManager.API.Infrastructure.Files;

public sealed class FileAssetClient(HttpClient httpClient) : IFileAssetClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<FileAssetSummary?> GetAsync(Guid id, CancellationToken ct)
    {
        var response = await httpClient.GetAsync($"/files/{id}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<FileAssetSummary>(JsonOptions, ct);
    }

    public async Task<byte[]> GetContentAsync(Guid id, CancellationToken ct)
    {
        var response = await httpClient.GetAsync($"/files/{id}/content", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(ct);
    }

    public async Task CommitAsync(Guid id, string ownerType, Guid ownerId, CancellationToken ct)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"/files/{id}/commit", new { ownerType, ownerId }, JsonOptions, ct);
        response.EnsureSuccessStatusCode();
    }
}
