using System.Globalization;
using System.Text;

namespace West94.ProxyManager.Files.Assets;

/// <summary>
/// Builds the single-bucket, two-prefix storage key scheme:
/// <c>staging/{uploadId:N}/{sanitizedFilename}</c> (uncommitted, sweeper-eligible) and
/// <c>{assetType}/{assetId:N}/{sanitizedFilename}</c> (committed). All lookups go by asset ID —
/// the filename exists only for Content-Disposition and console browsing.
/// </summary>
public static class AssetKeyBuilder
{
    private const int MaxFileNameLength = 200;
    private const string FallbackFileName = "asset.bin";

    public static string StagingKey(Guid uploadId, string fileName) =>
        $"staging/{uploadId:N}/{SanitizeFileName(fileName)}";

    public static string CommittedKey(string assetType, Guid assetId, string fileName) =>
        $"{assetType}/{assetId:N}/{SanitizeFileName(fileName)}";

    /// <summary>Strips separators, `..`, and control characters; NFC-normalizes; truncates; falls back if empty.</summary>
    public static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return FallbackFileName;
        }

        var normalized = fileName.Normalize(NormalizationForm.FormC);

        var builder = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (char.IsControl(c) || c is '/' or '\\')
            {
                continue;
            }
            builder.Append(c);
        }

        var cleaned = builder.ToString().Replace("..", string.Empty).Trim();

        if (cleaned.Length > MaxFileNameLength)
        {
            cleaned = cleaned[..MaxFileNameLength];
        }

        return string.IsNullOrWhiteSpace(cleaned) ? FallbackFileName : cleaned;
    }
}

/// <summary>Allowlist of asset types accepted by the service — prevents path traversal via an arbitrary <c>assetType</c> segment.</summary>
public static class AssetTypeAllowlist
{
    private static readonly HashSet<string> KnownTypes = new(StringComparer.Ordinal) { "certificate" };

    public static bool IsAllowed(string assetType) =>
        !string.IsNullOrWhiteSpace(assetType) && KnownTypes.Contains(assetType.ToLowerInvariant()) && assetType == assetType.ToLowerInvariant();

    public static string Normalize(string assetType)
    {
        var lower = assetType.ToLowerInvariant();
        if (!IsAllowed(lower))
        {
            throw new FileAssetValidationException($"Asset type '{assetType}' is not recognized.");
        }
        return lower;
    }
}
