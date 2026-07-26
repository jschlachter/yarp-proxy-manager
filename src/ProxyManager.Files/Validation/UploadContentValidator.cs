using Microsoft.Extensions.Options;

using West94.ProxyManager.Files.Options;

namespace West94.ProxyManager.Files.Validation;

/// <summary>
/// Three gates, never trusting the client header or extension alone: extension allowlist,
/// magic-byte sniffing, and a server-assigned content type derived from the two. Deeper X509
/// semantics (does the key match the cert, is the passphrase correct) do not live here.
/// </summary>
public sealed class UploadContentValidator(IOptions<UploadOptions> options)
{
    private static readonly byte[] PemPrefix = "-----BEGIN"u8.ToArray();

    public string Validate(string assetType, string fileName, ReadOnlySpan<byte> header)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!options.Value.AllowedExtensions.TryGetValue(assetType, out var allowed) || !allowed.Contains(extension))
        {
            throw new UnsupportedAssetContentException(
                $"Extension '{extension}' is not accepted for asset type '{assetType}'.");
        }

        if (IsPem(header))
        {
            return "application/x-pem-file";
        }

        if (IsDer(header))
        {
            return extension is ".pfx" or ".p12" ? "application/x-pkcs12" : "application/octet-stream";
        }

        throw new UnsupportedAssetContentException(
            "File content does not start with a recognized PEM or PKCS#12/DER header.");
    }

    private static bool IsPem(ReadOnlySpan<byte> header) =>
        header.Length >= PemPrefix.Length && header[..PemPrefix.Length].SequenceEqual(PemPrefix);

    private static bool IsDer(ReadOnlySpan<byte> header) =>
        header.Length >= 2 && header[0] == 0x30 && header[1] == 0x82;
}
