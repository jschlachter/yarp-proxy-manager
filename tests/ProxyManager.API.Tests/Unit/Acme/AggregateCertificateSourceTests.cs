extern alias ProxyManagerApp;
using ProxyManagerApp::West94.ProxyManager.Acme;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using West94.ProxyManager.API.Tests.Unit.Fakes;
using West94.ProxyManager.Core.AggregatesModel.CertificateAggregate;
using West94.ProxyManager.Infrastructure.Files;

namespace West94.ProxyManager.API.Tests.Unit.Acme;

[Trait("Category", "Unit")]
public class AggregateCertificateSourceTests
{
    private static (AggregateCertificateSource Source, FakeCertificateRepository Certs, FakeFileAssetClient Files) CreateSource(
        Func<FakeFileAssetClient, IFileAssetClient>? wrapFiles = null)
    {
        var certs = new FakeCertificateRepository();
        var files = new FakeFileAssetClient();
        var services = new ServiceCollection();
        services.AddScoped<ICertificateRepository>(_ => certs);
        services.AddScoped<IFileAssetClient>(_ => wrapFiles?.Invoke(files) ?? files);
        var sp = services.BuildServiceProvider();
        var retry = Microsoft.Extensions.Options.Options.Create(new FilesRetryOptions { MaxAttempts = 3, InitialDelay = TimeSpan.Zero, MaxDelay = TimeSpan.Zero });
        var source = new AggregateCertificateSource(
            sp.GetRequiredService<IServiceScopeFactory>(), retry, NullLogger<AggregateCertificateSource>.Instance);
        return (source, certs, files);
    }

    private sealed class FlakyFileAssetClient(FakeFileAssetClient inner, int failures, Exception failure) : IFileAssetClient
    {
        public int ContentCalls { get; private set; }

        public Task<byte[]> GetContentAsync(Guid id, CancellationToken ct)
        {
            ContentCalls++;
            return ContentCalls <= failures ? throw failure : inner.GetContentAsync(id, ct);
        }

        public Task<FileAssetSummary?> GetAsync(Guid id, CancellationToken ct) => inner.GetAsync(id, ct);
        public Task CommitAsync(Guid id, string ownerType, Guid ownerId, CancellationToken ct) => inner.CommitAsync(id, ownerType, ownerId, ct);
        public Task<Guid> UploadAsync(string fileName, string contentType, Stream content, CancellationToken ct) => inner.UploadAsync(fileName, contentType, content, ct);
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
    public async Task GetCertificateAsync_WildcardSanMatchesSubdomain()
    {
        var (source, certs, files) = CreateSource();
        var pfxBytes = TestCertificateGenerator.CreatePfx();
        var cert = Certificate.Create("wildcard", CertificateFormat.Pfx, Guid.NewGuid(), null, "wildcard.pfx", null, null, Subject("*.example.com"));
        certs.Seed(cert);
        files.Seed(cert.CertificateAssetId, "wildcard.pfx", pfxBytes);

        var result = await source.GetCertificateAsync("proxy-manager.example.com", CancellationToken.None);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetCertificateAsync_WildcardSanDoesNotMatchBareDomain()
    {
        var (source, certs, _) = CreateSource();
        certs.Seed(Certificate.Create("wildcard", CertificateFormat.Pfx, Guid.NewGuid(), null, "wildcard.pfx", null, null, Subject("*.example.com")));

        var result = await source.GetCertificateAsync("example.com", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetCertificateAsync_WildcardSanDoesNotMatchMultiLevelSubdomain()
    {
        var (source, certs, _) = CreateSource();
        certs.Seed(Certificate.Create("wildcard", CertificateFormat.Pfx, Guid.NewGuid(), null, "wildcard.pfx", null, null, Subject("*.example.com")));

        var result = await source.GetCertificateAsync("a.b.example.com", CancellationToken.None);

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

    [Fact]
    public async Task GetCertificatesAsync_RetriesTransientFilesFailureThenLoads()
    {
        FlakyFileAssetClient? flaky = null;
        var (source, certs, files) = CreateSource(f => flaky = new FlakyFileAssetClient(f, 2, new HttpRequestException("Connection refused")));
        var cert = Certificate.Create("retry", CertificateFormat.Pfx, Guid.NewGuid(), null, "retry.pfx", null, null, Subject("retry.example.com"));
        certs.Seed(cert);
        files.Seed(cert.CertificateAssetId, "retry.pfx", TestCertificateGenerator.CreatePfx());

        var result = (await source.GetCertificatesAsync(CancellationToken.None)).ToList();

        Assert.Single(result);
        Assert.Equal(3, flaky!.ContentCalls);
    }

    [Fact]
    public async Task GetCertificatesAsync_GivesUpAfterMaxAttempts()
    {
        FlakyFileAssetClient? flaky = null;
        var (source, certs, files) = CreateSource(f => flaky = new FlakyFileAssetClient(f, int.MaxValue, new HttpRequestException("Connection refused")));
        var cert = Certificate.Create("down", CertificateFormat.Pfx, Guid.NewGuid(), null, "down.pfx", null, null, Subject("down.example.com"));
        certs.Seed(cert);
        files.Seed(cert.CertificateAssetId, "down.pfx", TestCertificateGenerator.CreatePfx());

        var result = (await source.GetCertificatesAsync(CancellationToken.None)).ToList();

        Assert.Empty(result);
        Assert.Equal(3, flaky!.ContentCalls);
    }

    [Fact]
    public async Task GetCertificatesAsync_DoesNotRetryClientErrors()
    {
        FlakyFileAssetClient? flaky = null;
        var (source, certs, files) = CreateSource(f => flaky = new FlakyFileAssetClient(
            f, int.MaxValue, new HttpRequestException("Not found", null, System.Net.HttpStatusCode.NotFound)));
        var cert = Certificate.Create("missing", CertificateFormat.Pfx, Guid.NewGuid(), null, "missing.pfx", null, null, Subject("missing.example.com"));
        certs.Seed(cert);
        files.Seed(cert.CertificateAssetId, "missing.pfx", TestCertificateGenerator.CreatePfx());

        var result = (await source.GetCertificatesAsync(CancellationToken.None)).ToList();

        Assert.Empty(result);
        Assert.Equal(1, flaky!.ContentCalls);
    }
}
