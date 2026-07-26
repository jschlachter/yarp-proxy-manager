using West94.ProxyManager.Files.Assets;

namespace West94.ProxyManager.Files.Contracts;

/// <summary>
/// Deliberately excludes <c>StorageKey</c> — leaking it invites callers to bypass the service,
/// the same mistake <c>CertificatePath</c> made. All lookups go by <see cref="Id"/>.
/// </summary>
public sealed record FileAssetDto(
    Guid Id,
    string AssetType,
    string FileName,
    string ContentType,
    long SizeBytes,
    string Sha256,
    string Status,
    string? OwnerType,
    Guid? OwnerId,
    string UploadedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CommittedAt);

public static class FileAssetDtoExtensions
{
    public static FileAssetDto ToDto(this FileAsset asset) => new(
        asset.Id,
        asset.AssetType,
        asset.FileName,
        asset.ContentType,
        asset.SizeBytes,
        asset.Sha256,
        asset.Status.ToString(),
        asset.OwnerType,
        asset.OwnerId,
        asset.UploadedBy,
        asset.CreatedAt,
        asset.CommittedAt);
}
