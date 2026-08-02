using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

using West94.ProxyManager.Core.AggregatesModel.CertificateAggregate;
using West94.ProxyManager.Core.Certificates;
using West94.ProxyManager.Core.Exceptions;

namespace West94.ProxyManager.Core.Tests.Unit;

/// <summary>
/// Fixture certs are generated at test time via <see cref="CertificateRequest"/> rather than
/// committed as binary blobs — equivalent coverage without checking binary fixtures into git,
/// and it makes the mismatched-key / wrong-passphrase / expired scenarios trivial to construct.
/// </summary>
[Trait("Category", "Unit")]
public class X509CertificateInspectorTests
{
    private static (X509Certificate2 Cert, RSA Key) CreateSelfSigned(
        string subject = "CN=test.example.com", DateTimeOffset? notBefore = null, DateTimeOffset? notAfter = null)
    {
        var rsa = RSA.Create(2048);
        var request = new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName("test.example.com");
        san.AddDnsName("www.test.example.com");
        request.CertificateExtensions.Add(san.Build());

        var cert = request.CreateSelfSigned(
            notBefore ?? DateTimeOffset.UtcNow.AddDays(-1),
            notAfter ?? DateTimeOffset.UtcNow.AddYears(1));
        return (cert, rsa);
    }

    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    [Fact]
    public void Inspect_Pem_MatchedKey_ReturnsSubjectInfo()
    {
        var (cert, rsa) = CreateSelfSigned();
        var certPem = Utf8(cert.ExportCertificatePem());
        var keyPem = Utf8(rsa.ExportRSAPrivateKeyPem());

        var result = X509CertificateInspector.Inspect(certPem, keyPem, CertificateFormat.Pem, passPhrase: null);

        Assert.Equal("CN=test.example.com", result.Subject);
        Assert.Contains("test.example.com", result.SubjectAlternativeNames);
        Assert.Contains("www.test.example.com", result.SubjectAlternativeNames);
        Assert.Equal(cert.Thumbprint, result.Thumbprint);
    }

    [Fact]
    public void Inspect_Pem_CertOnly_NoKey_ReturnsSubjectInfo()
    {
        var (cert, _) = CreateSelfSigned();
        var certPem = Utf8(cert.ExportCertificatePem());

        var result = X509CertificateInspector.Inspect(certPem, ReadOnlySpan<byte>.Empty, CertificateFormat.Pem, passPhrase: null);

        Assert.Equal(cert.Thumbprint, result.Thumbprint);
    }

    [Fact]
    public void Inspect_Pem_MismatchedKey_ThrowsValidationException()
    {
        var (cert, _) = CreateSelfSigned();
        using var otherRsa = RSA.Create(2048);
        var certPem = Utf8(cert.ExportCertificatePem());
        var wrongKeyPem = Utf8(otherRsa.ExportRSAPrivateKeyPem());

        Assert.Throws<CertificateValidationException>(() =>
            X509CertificateInspector.Inspect(certPem, wrongKeyPem, CertificateFormat.Pem, passPhrase: null));
    }

    [Fact]
    public void Inspect_Pem_EncryptedKey_CorrectPassphrase_ReturnsSubjectInfo()
    {
        var (cert, rsa) = CreateSelfSigned();
        var certPem = Utf8(cert.ExportCertificatePem());
        var encryptedKeyPem = Utf8(rsa.ExportEncryptedPkcs8PrivateKeyPem(
            "correct-horse", new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 100_000)));

        var result = X509CertificateInspector.Inspect(certPem, encryptedKeyPem, CertificateFormat.Pem, "correct-horse");

        Assert.Equal(cert.Thumbprint, result.Thumbprint);
    }

    [Fact]
    public void Inspect_Pem_EncryptedKey_WrongPassphrase_ThrowsValidationException()
    {
        var (cert, rsa) = CreateSelfSigned();
        var certPem = Utf8(cert.ExportCertificatePem());
        var encryptedKeyPem = Utf8(rsa.ExportEncryptedPkcs8PrivateKeyPem(
            "correct-horse", new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 100_000)));

        Assert.Throws<CertificateValidationException>(() =>
            X509CertificateInspector.Inspect(certPem, encryptedKeyPem, CertificateFormat.Pem, "wrong-passphrase"));
    }

    [Fact]
    public void Inspect_Pfx_CorrectPassphrase_ReturnsSubjectInfo()
    {
        var (cert, _) = CreateSelfSigned();
        var pfxBytes = cert.Export(X509ContentType.Pfx, "secret");

        var result = X509CertificateInspector.Inspect(pfxBytes, ReadOnlySpan<byte>.Empty, CertificateFormat.Pfx, "secret");

        Assert.Equal(cert.Thumbprint, result.Thumbprint);
    }

    [Fact]
    public void Inspect_Pfx_WrongPassphrase_ThrowsValidationException()
    {
        var (cert, _) = CreateSelfSigned();
        var pfxBytes = cert.Export(X509ContentType.Pfx, "secret");

        Assert.Throws<CertificateValidationException>(() =>
            X509CertificateInspector.Inspect(pfxBytes, ReadOnlySpan<byte>.Empty, CertificateFormat.Pfx, "wrong"));
    }

    [Fact]
    public void Inspect_ExpiredCertificate_DoesNotThrow_WarnNotFail()
    {
        var (cert, rsa) = CreateSelfSigned(
            notBefore: DateTimeOffset.UtcNow.AddYears(-2), notAfter: DateTimeOffset.UtcNow.AddDays(-1));
        var certPem = Utf8(cert.ExportCertificatePem());
        var keyPem = Utf8(rsa.ExportRSAPrivateKeyPem());

        var result = X509CertificateInspector.Inspect(certPem, keyPem, CertificateFormat.Pem, passPhrase: null);

        Assert.True(result.NotAfter < DateTimeOffset.UtcNow);
    }
}
