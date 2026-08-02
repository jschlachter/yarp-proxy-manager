using West94.ProxyManager.API.Handlers;
using West94.ProxyManager.API.Tests.Unit.Fakes;
using West94.ProxyManager.Core.AggregatesModel.CertificateAggregate;
using West94.ProxyManager.Core.AggregatesModel.ProxyHostAggregate;
using West94.ProxyManager.Core.Exceptions;
using West94.ProxyManager.Core.Messages.Commands;
using West94.ProxyManager.Core.Messages.Events;

namespace West94.ProxyManager.API.Tests.Unit.Handlers;

[Trait("Category", "Unit")]
public class AssignCertificateHandlerTests
{
    private static AssignCertificateHandler MakeHandler(
        FakeProxyHostRepository hostRepo, FakeCertificateRepository certRepo) =>
        new(hostRepo, certRepo);

    private static ProxyHost MakeHost()
    {
        var host = ProxyHost.Create(["assign-test.example.com"], DestinationUri.Parse("http://backend:8080"));
        return host;
    }

    private static Certificate MakeCert() =>
        TestCertificates.Create("test-cert", CertificateFormat.Pem);

    [Fact]
    public async Task Handle_AssignValidCert_UpdatesHostCertificateId()
    {
        var hostRepo = new FakeProxyHostRepository();
        var certRepo = new FakeCertificateRepository();
        var host = MakeHost();
        var cert = MakeCert();
        hostRepo.Seed(host);
        certRepo.Seed(cert);
        var handler = MakeHandler(hostRepo, certRepo);

        var command = new AssignCertificateCommand(host.Id, cert.Id, "actor-1");
        var (dto, _) = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(cert.Id, dto.CertificateId);
    }

    [Fact]
    public async Task Handle_AssignValidCert_ReturnsProxyHostUpdatedEvent()
    {
        var hostRepo = new FakeProxyHostRepository();
        var certRepo = new FakeCertificateRepository();
        var host = MakeHost();
        var cert = MakeCert();
        hostRepo.Seed(host);
        certRepo.Seed(cert);
        var handler = MakeHandler(hostRepo, certRepo);

        var (_, @event) = await handler.Handle(
            new AssignCertificateCommand(host.Id, cert.Id, "actor-1"), CancellationToken.None);

        Assert.IsType<ProxyHostUpdatedEvent>(@event);
        Assert.Equal(host.Id, @event.Id);
    }

    [Fact]
    public async Task Handle_UnassignCert_SetsCertificateIdToNull()
    {
        var hostRepo = new FakeProxyHostRepository();
        var certRepo = new FakeCertificateRepository();
        var cert = MakeCert();
        var host = ProxyHost.Create(["unassign-test.example.com"], DestinationUri.Parse("http://backend:8080"), cert.Id);
        hostRepo.Seed(host);
        certRepo.Seed(cert);
        var handler = MakeHandler(hostRepo, certRepo);

        var command = new AssignCertificateCommand(host.Id, null, "actor-1");
        var (dto, _) = await handler.Handle(command, CancellationToken.None);

        Assert.Null(dto.CertificateId);
    }

    [Fact]
    public async Task Handle_UnknownHost_ThrowsProxyHostNotFoundException()
    {
        var hostRepo = new FakeProxyHostRepository();
        var certRepo = new FakeCertificateRepository();
        var handler = MakeHandler(hostRepo, certRepo);

        await Assert.ThrowsAsync<ProxyHostNotFoundException>(() =>
            handler.Handle(new AssignCertificateCommand(Guid.NewGuid(), null, "actor-1"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_UnknownCertId_ThrowsCertificateNotFoundException()
    {
        var hostRepo = new FakeProxyHostRepository();
        var certRepo = new FakeCertificateRepository();
        var host = MakeHost();
        hostRepo.Seed(host);
        var handler = MakeHandler(hostRepo, certRepo);

        await Assert.ThrowsAsync<CertificateNotFoundException>(() =>
            handler.Handle(new AssignCertificateCommand(host.Id, Guid.NewGuid(), "actor-1"), CancellationToken.None));
    }
}
