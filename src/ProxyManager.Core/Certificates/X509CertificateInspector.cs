using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

using West94.ProxyManager.Core.AggregatesModel.CertificateAggregate;
using West94.ProxyManager.Core.Exceptions;

namespace West94.ProxyManager.Core.Certificates;

/// <summary>
/// Parses uploaded certificate bytes and extracts subject metadata. Pure and static — no I/O, no
/// host — so it is unit-testable with fixture bytes and reusable later from <c>ProxyManager</c>
/// for SNI cert selection. "Does the key match the cert" is cert-domain knowledge, kept out of the
/// generic Files service and out of the API endpoint handler (untestable there without a web host).
/// </summary>
public static class X509CertificateInspector
{
    /// <summary>
    /// Verifies the key matches the cert (for PEM) and the passphrase decrypts (for PFX), then
    /// extracts subject/SANs/validity/thumbprint. Warns rather than fails on already-expired certs —
    /// uploading a soon-to-be-renewed certificate is a legitimate workflow.
    /// </summary>
    public static CertificateSubjectInfo Inspect(
        ReadOnlySpan<byte> certBytes, ReadOnlySpan<byte> keyBytes,
        CertificateFormat format, string? passPhrase)
    {
        using var cert = format switch
        {
            CertificateFormat.Pfx => LoadPfx(certBytes, passPhrase),
            CertificateFormat.Pem => LoadPem(certBytes, keyBytes, passPhrase),
            _ => throw new CertificateValidationException($"Unsupported certificate format '{format}'."),
        };

        return new CertificateSubjectInfo(
            cert.Subject,
            ExtractSubjectAlternativeNames(cert),
            new DateTimeOffset(cert.NotBefore.ToUniversalTime()),
            new DateTimeOffset(cert.NotAfter.ToUniversalTime()),
            cert.Thumbprint);
    }

    private static X509Certificate2 LoadPfx(ReadOnlySpan<byte> certBytes, string? passPhrase)
    {
        try
        {
            return X509CertificateLoader.LoadPkcs12(certBytes, passPhrase);
        }
        catch (CryptographicException ex)
        {
            throw new CertificateValidationException(
                "Unable to load the PFX/PKCS#12 certificate — the passphrase may be incorrect or the file is corrupt.")
            {
                Data = { ["Inner"] = ex.Message },
            };
        }
    }

    private static X509Certificate2 LoadPem(ReadOnlySpan<byte> certBytes, ReadOnlySpan<byte> keyBytes, string? passPhrase)
    {
        var certPem = Encoding.UTF8.GetString(certBytes);

        if (keyBytes.IsEmpty)
        {
            try
            {
                return X509Certificate2.CreateFromPem(certPem);
            }
            catch (CryptographicException ex)
            {
                throw new CertificateValidationException("Unable to parse the PEM certificate.") { Data = { ["Inner"] = ex.Message } };
            }
        }

        var keyPem = Encoding.UTF8.GetString(keyBytes);

        try
        {
            return string.IsNullOrEmpty(passPhrase)
                ? X509Certificate2.CreateFromPem(certPem, keyPem)
                : X509Certificate2.CreateFromEncryptedPem(certPem, keyPem, passPhrase);
        }
        catch (CryptographicException ex)
        {
            throw new CertificateValidationException(
                "The private key does not match the certificate, the passphrase is incorrect, or the PEM data is malformed.")
            {
                Data = { ["Inner"] = ex.Message },
            };
        }
    }

    private static IReadOnlyList<string> ExtractSubjectAlternativeNames(X509Certificate2 cert) =>
        cert.Extensions.OfType<X509SubjectAlternativeNameExtension>()
            .FirstOrDefault()
            ?.EnumerateDnsNames()
            .ToList()
        ?? [];
}
