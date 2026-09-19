using West94.ProxyManager.API.Handlers;
using West94.ProxyManager.API.Tests.Unit.Fakes;
using West94.ProxyManager.Core.AggregatesModel.CertificateAggregate;
using West94.ProxyManager.Core.Messages.Queries;

namespace West94.ProxyManager.API.Tests.Unit.Handlers;

[Trait("Category", "Unit")]
public class GetCertificatesHandlerTests
{
    [Fact]
    public async Task Handle_ManualCertificate_ReturnsManualSourceInDto()
    {
        var repo = new FakeCertificateRepository();
        repo.Seed(TestCertificates.Create("manual-cert", CertificateFormat.Pem, keyAssetId: Guid.NewGuid()));
        var handler = new GetCertificatesHandler(repo);

        var result = await handler.Handle(new GetCertificatesQuery(1, 20), CancellationToken.None);

        var dto = Assert.Single(result.Items);
        Assert.Equal("Manual", dto.Source);
    }

    [Fact]
    public async Task Handle_LetsEncryptCertificate_ReturnsLetsEncryptSourceInDto()
    {
        var repo = new FakeCertificateRepository();
        var subject = TestCertificates.MakeSubject();
        var cert = Certificate.Create(
            "le-cert", CertificateFormat.Pem, Guid.NewGuid(), Guid.NewGuid(), "cert.pem", "key.pem", null, subject,
            source: CertificateSource.LetsEncrypt);
        repo.Seed(cert);
        var handler = new GetCertificatesHandler(repo);

        var result = await handler.Handle(new GetCertificatesQuery(1, 20), CancellationToken.None);

        var dto = Assert.Single(result.Items);
        Assert.Equal("LetsEncrypt", dto.Source);
    }
}
