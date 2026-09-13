extern alias ProxyManagerApp;
using ProxyManagerApp::West94.ProxyManager.Acme;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using West94.ProxyManager.API.Tests.Unit.Fakes;
using West94.ProxyManager.Core.AggregatesModel.CertificateAggregate;
using West94.ProxyManager.Infrastructure.Files;

namespace West94.ProxyManager.API.Tests.Unit.Acme;

[Trait("Category", "Unit")]
public class AggregateCertificateSourceTests
{
    private static (AggregateCertificateSource Source, FakeCertificateRepository Certs, FakeFileAssetClient Files) CreateSource()
    {
        var certs = new FakeCertificateRepository();
        var files = new FakeFileAssetClient();
        var services = new ServiceCollection();
        services.AddScoped<ICertificateRepository>(_ => certs);
        services.AddScoped<IFileAssetClient>(_ => files);
        var sp = services.BuildServiceProvider();
        var source = new AggregateCertificateSource(
            sp.GetRequiredService<IServiceScopeFactory>(), NullLogger<AggregateCertificateSource>.Instance);
        return (source, certs, files);
    }

    private static CertificateSubjectInfo Subject(params string[] sans) =>
        new("CN=" + sans[0], sans, DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1), "ABCDEF0123456789");

    [Fact]
    public async Task GetCertificateAsync_WithMatchingSan_ReturnsCertificate()
    {
        var (source, certs, files) = CreateSource();
        var pfxBytes = TestCertificateGenerator.CreatePfx();
        var cert = Certificate.Create("match", CertificateFormat.Pfx, Guid.NewGuid(), null, "match.pfx", null, null, Subject("match.example.com"));
        certs.Seed(cert);
        files.Seed(cert.CertificateAssetId, "match.pfx", pfxBytes);

        var result = await source.GetCertificateAsync("match.example.com", CancellationToken.None);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetCertificateAsync_IsCaseInsensitive()
    {
        var (source, certs, files) = CreateSource();
        var pfxBytes = TestCertificateGenerator.CreatePfx();
        var cert = Certificate.Create("match", CertificateFormat.Pfx, Guid.NewGuid(), null, "match.pfx", null, null, Subject("Match.Example.com"));
        certs.Seed(cert);
        files.Seed(cert.CertificateAssetId, "match.pfx", pfxBytes);

        var result = await source.GetCertificateAsync("match.example.com", CancellationToken.None);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetCertificateAsync_WithNoMatch_ReturnsNull()
    {
        var (source, certs, _) = CreateSource();
        certs.Seed(Certificate.Create("other", CertificateFormat.Pfx, Guid.NewGuid(), null, "other.pfx", null, null, Subject("other.example.com")));

        var result = await source.GetCertificateAsync("nomatch.example.com", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetCertificatesAsync_SkipsMalformedRowWithoutThrowing()
    {
        var (source, certs, files) = CreateSource();
        var goodPfx = TestCertificateGenerator.CreatePfx();
        var goodCert = Certificate.Create("good", CertificateFormat.Pfx, Guid.NewGuid(), null, "good.pfx", null, null, Subject("good.example.com"));
        var badCert = Certificate.Create("bad", CertificateFormat.Pfx, Guid.NewGuid(), null, "bad.pfx", null, null, Subject("bad.example.com"));
        certs.Seed(goodCert, badCert);
        files.Seed(goodCert.CertificateAssetId, "good.pfx", goodPfx);
        files.Seed(badCert.CertificateAssetId, "bad.pfx", [0x01, 0x02, 0x03]); // not a valid PFX

        var result = (await source.GetCertificatesAsync(CancellationToken.None)).ToList();

        Assert.Single(result);
    }

    [Fact]
    public async Task GetCertificatesAsync_LoadsPemCertWithKey()
    {
        var (source, certs, files) = CreateSource();
        var (certPem, keyPem) = TestCertificateGenerator.CreatePemPair();
        var cert = Certificate.Create(
            "pem-cert", CertificateFormat.Pem, Guid.NewGuid(), Guid.NewGuid(), "cert.pem", "key.pem", null, Subject("pem.example.com"));
        certs.Seed(cert);
        files.Seed(cert.CertificateAssetId, "cert.pem", certPem);
        files.Seed(cert.KeyAssetId!.Value, "key.pem", keyPem);

        var result = (await source.GetCertificatesAsync(CancellationToken.None)).ToList();

        Assert.Single(result);
    }
}
