using West94.ProxyManager.Core.AggregatesModel.CertificateAggregate;
using West94.ProxyManager.Core.Exceptions;

namespace West94.ProxyManager.Core.Tests.Unit;

[Trait("Category", "Unit")]
public class CertificateAggregateTests
{
    private static CertificateSubjectInfo Subject() =>
        new("CN=example.com", ["example.com"], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(90), "THUMBPRINT");

    [Fact]
    public void Create_WithoutExplicitSource_DefaultsToManual()
    {
        var certificate = Certificate.Create("cert", CertificateFormat.Pfx, Guid.NewGuid(), null,
            "cert.pfx", null, null, Subject());

        Assert.Equal(CertificateSource.Manual, certificate.Source);
    }

    [Fact]
    public void Create_WithLetsEncryptSource_SetsSource()
    {
        var certificate = Certificate.Create("cert", CertificateFormat.Pem, Guid.NewGuid(), Guid.NewGuid(),
            "cert.pem", "key.pem", null, Subject(), source: CertificateSource.LetsEncrypt);

        Assert.Equal(CertificateSource.LetsEncrypt, certificate.Source);
    }

    [Fact]
    public void ReplaceAssets_WithValidPemArgs_UpdatesAssetsAndTouchesUpdatedAt()
    {
        var certificate = Certificate.Create("cert", CertificateFormat.Pem, Guid.NewGuid(), Guid.NewGuid(),
            "cert.pem", "key.pem", null, Subject());
        var originalUpdatedAt = certificate.UpdatedAt;
        var newCertAssetId = Guid.NewGuid();
        var newKeyAssetId = Guid.NewGuid();
        var newSubject = new CertificateSubjectInfo("CN=renewed.example.com", ["renewed.example.com"],
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(90), "NEWTHUMBPRINT");

        certificate.ReplaceAssets(newCertAssetId, newKeyAssetId, "renewed.pem", "renewed.key.pem", newSubject);

        Assert.Equal(newCertAssetId, certificate.CertificateAssetId);
        Assert.Equal(newKeyAssetId, certificate.KeyAssetId);
        Assert.Equal("renewed.pem", certificate.CertificateFileName);
        Assert.Equal("renewed.key.pem", certificate.KeyFileName);
        Assert.Equal(newSubject, certificate.Subject);
        Assert.True(certificate.UpdatedAt >= originalUpdatedAt);
    }

    [Fact]
    public void ReplaceAssets_WithPfxFormatAndNonNullKeyAssetId_ThrowsCertificateValidationException()
    {
        var certificate = Certificate.Create("cert", CertificateFormat.Pfx, Guid.NewGuid(), null,
            "cert.pfx", null, null, Subject());

        Assert.Throws<CertificateValidationException>(() =>
            certificate.ReplaceAssets(Guid.NewGuid(), Guid.NewGuid(), "cert.pfx", null, Subject()));
    }

    [Fact]
    public void ReplaceAssets_WithEmptyCertificateAssetId_ThrowsCertificateValidationException()
    {
        var certificate = Certificate.Create("cert", CertificateFormat.Pem, Guid.NewGuid(), Guid.NewGuid(),
            "cert.pem", "key.pem", null, Subject());

        Assert.Throws<CertificateValidationException>(() =>
            certificate.ReplaceAssets(Guid.Empty, Guid.NewGuid(), "cert.pem", "key.pem", Subject()));
    }
}
