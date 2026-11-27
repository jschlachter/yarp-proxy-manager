using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using LettuceEncrypt;
using Microsoft.Extensions.Options;
using West94.ProxyManager.Options;

namespace West94.ProxyManager.Acme;

/// <summary>
/// Loads the PFX described by <see cref="FallbackCertificateOptions"/> into
/// <see cref="LettuceEncryptOptions.FallbackCertificate"/>. Without it, LettuceEncrypt hands Kestrel a
/// null certificate whenever it has none for the requested host, and every such handshake fails with
/// "The server mode SSL must use a certificate with the associated private key" — including, on an
/// empty database, the management host itself, which locks out the UI needed to upload one.
/// A missing or unusable file is logged and skipped rather than failing startup: user routes with
/// their own certificates must keep working.
/// </summary>
public sealed class FallbackCertificateSetup(
    IOptions<FallbackCertificateOptions> fallbackOptions,
    IOptions<ManagementOptions> managementOptions,
    IHostEnvironment environment,
    ILogger<FallbackCertificateSetup> logger) : IConfigureOptions<LettuceEncryptOptions>
{
    /// <summary>Sets the fallback certificate unless one is already configured.</summary>
    public void Configure(LettuceEncryptOptions options)
    {
        if (options.FallbackCertificate is not null)
        {
            return;
        }

        var path = ResolvePath();
        if (path is null)
        {
            logger.LogWarning("No fallback TLS certificate path could be determined; handshakes without a matching certificate will fail.");
            return;
        }

        if (!File.Exists(path))
        {
            logger.LogWarning("Fallback TLS certificate {Path} not found; handshakes without a matching certificate will fail.", path);
            return;
        }

        try
        {
            var certificate = X509CertificateLoader.LoadPkcs12FromFile(path, fallbackOptions.Value.Password);
            if (!certificate.HasPrivateKey)
            {
                logger.LogWarning("Fallback TLS certificate {Path} has no private key; ignoring it.", path);
                certificate.Dispose();
                return;
            }

            options.FallbackCertificate = certificate;
            logger.LogInformation("Loaded fallback TLS certificate {Path} ({Subject}, expires {NotAfter:O}).",
                path, certificate.Subject, certificate.NotAfter);
        }
        catch (CryptographicException ex)
        {
            logger.LogWarning(ex, "Fallback TLS certificate {Path} could not be loaded (wrong password or not a PFX?); ignoring it.", path);
        }
    }

    /// <summary>
    /// The configured path made absolute against the content root, or the
    /// <c>certs/{management host}.pfx</c> convention when no path is configured.
    /// </summary>
    private string? ResolvePath()
    {
        var configured = fallbackOptions.Value.Path;
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return Path.GetFullPath(configured, environment.ContentRootPath);
        }

        var host = managementOptions.Value.Hosts.FirstOrDefault(h => !string.IsNullOrWhiteSpace(h));
        return host is null ? null : Path.Combine(environment.ContentRootPath, "certs", $"{host}.pfx");
    }
}
