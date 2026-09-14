extern alias ProxyManagerApp;
using ProxyManagerApp::West94.ProxyManager.Acme;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using West94.ProxyManager.API.Tests.Unit.Fakes;
using West94.ProxyManager.Core.AggregatesModel.CertificateAggregate;
using West94.ProxyManager.Core.AggregatesModel.ProxyHostAggregate;
using West94.ProxyManager.Infrastructure.Files;

namespace West94.ProxyManager.API.Tests.Unit.Acme;

[Trait("Category", "Unit")]
public class AggregateCertificateRepositoryTests
{
    private static (AggregateCertificateRepository Repo, FakeCertificateRepository Certs, FakeProxyHostRepository Hosts, FakeFileAssetClient Files) CreateRepository()
    {
        var certs = new FakeCertificateRepository();
        var hosts = new FakeProxyHostRepository();
        var files = new FakeFileAssetClient();
        var services = new ServiceCollection();
        services.AddScoped<ICertificateRepository>(_ => certs);
        services.AddScoped<IProxyHostRepository>(_ => hosts);
        services.AddScoped<IFileAssetClient>(_ => files);
        var sp = services.BuildServiceProvider();
        var repo = new AggregateCertificateRepository(
            sp.GetRequiredService<IServiceScopeFactory>(), NullLogger<AggregateCertificateRepository>.Instance);
        return (repo, certs, hosts, files);
    }

    [Fact]
    public async Task SaveAsync_FirstIssuance_CreatesLetsEncryptCertificateAndAssignsMatchingHost()
    {
        var (repo, certs, hosts, files) = CreateRepository();
        var host = ProxyHost.Create(["issued.example.com"], DestinationUri.Parse("http://backend:8080"), tlsMode: TlsMode.LetsEncrypt);
        hosts.Seed(host);

        using var x509 = MakeSelfSignedCert("issued.example.com");
        await repo.SaveAsync(x509, CancellationToken.None);

        var allCerts = await certs.GetAllAsync();
        var created = Assert.Single(allCerts);
        Assert.Equal(CertificateSource.LetsEncrypt, created.Source);
        Assert.Single(files.Uploads);

        var reloadedHost = await hosts.FindAsync(host.Id);
        Assert.Equal(created.Id, reloadedHost!.CertificateId);
    }

    [Fact]
    public async Task SaveAsync_RenewalOfExistingLetsEncryptCert_ReplacesAssetsKeepingSameId()
    {
        var (repo, certs, hosts, files) = CreateRepository();
        var host = ProxyHost.Create(["renew.example.com"], DestinationUri.Parse("http://backend:8080"), tlsMode: TlsMode.LetsEncrypt);
        hosts.Seed(host);

        using var firstCert = MakeSelfSignedCert("renew.example.com");
        await repo.SaveAsync(firstCert, CancellationToken.None);
        var originalId = Assert.Single(await certs.GetAllAsync()).Id;

        using var renewedCert = MakeSelfSignedCert("renew.example.com");
        await repo.SaveAsync(renewedCert, CancellationToken.None);

        var allCerts = await certs.GetAllAsync();
        var renewed = Assert.Single(allCerts);
        Assert.Equal(originalId, renewed.Id);
        Assert.Equal(2, files.Uploads.Count);
    }

    [Fact]
    public async Task SaveAsync_ManualCertificateWithSameDomain_IsNotTreatedAsRenewal()
    {
        var (repo, certs, hosts, files) = CreateRepository();
        var manualCert = TestCertificates.Create("manual", CertificateFormat.Pem, keyAssetId: Guid.NewGuid());
        certs.Seed(manualCert);

        using var x509 = MakeSelfSignedCert("test.example.com");
        await repo.SaveAsync(x509, CancellationToken.None);

        var allCerts = await certs.GetAllAsync();
        Assert.Equal(2, allCerts.Count);
        Assert.Contains(allCerts, c => c.Source == CertificateSource.LetsEncrypt);
    }

    private static System.Security.Cryptography.X509Certificates.X509Certificate2 MakeSelfSignedCert(string domain)
    {
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var request = new System.Security.Cryptography.X509Certificates.CertificateRequest(
            $"CN={domain}", rsa, System.Security.Cryptography.HashAlgorithmName.SHA256, System.Security.Cryptography.RSASignaturePadding.Pkcs1);
        var sanBuilder = new System.Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder();
        sanBuilder.AddDnsName(domain);
        request.CertificateExtensions.Add(sanBuilder.Build());
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
    }
}
