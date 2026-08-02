using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace West94.ProxyManager.API.Tests.Unit.Fakes;

/// <summary>Generates real, ephemeral self-signed certificates for tests that need bytes X509CertificateInspector can actually parse.</summary>
internal static class TestCertificateGenerator
{
    public static (byte[] CertPem, byte[] KeyPem) CreatePemPair(string subjectName = "CN=test.example.com")
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(subjectName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));

        var certPem = System.Text.Encoding.UTF8.GetBytes(cert.ExportCertificatePem());
        var keyPem = System.Text.Encoding.UTF8.GetBytes(rsa.ExportRSAPrivateKeyPem());
        return (certPem, keyPem);
    }

    public static byte[] CreatePfx(string subjectName = "CN=test.example.com", string? password = null)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(subjectName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
        return cert.Export(X509ContentType.Pfx, password);
    }
}
