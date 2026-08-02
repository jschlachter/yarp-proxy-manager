using West94.ProxyManager.API.Handlers;
using West94.ProxyManager.API.Tests.Unit.Fakes;
using West94.ProxyManager.Core.AggregatesModel.CertificateAggregate;
using West94.ProxyManager.Core.Exceptions;
using West94.ProxyManager.Core.Messages.Commands;
using West94.ProxyManager.Core.Messages.Events;

namespace West94.ProxyManager.API.Tests.Unit.Handlers;

[Trait("Category", "Unit")]
public class UpdateCertificateHandlerTests
{
    private static Certificate SeedCert(FakeCertificateRepository repo, string name = "original-name")
    {
        var cert = TestCertificates.Create(name, CertificateFormat.Pem);
        repo.Seed(cert);
        return cert;
    }

    [Fact]
    public async Task Handle_RenameOnly_UpdatesNameInDto()
    {
        var repo = new FakeCertificateRepository();
        var cert = SeedCert(repo);
        var handler = new UpdateCertificateHandler(repo);

        var command = new UpdateCertificateCommand(cert.Id, "new-name", null, "actor-1");
        var (dto, _) = await handler.Handle(command, CancellationToken.None);

        Assert.Equal("new-name", dto.Name);
    }

    [Fact]
    public async Task Handle_PassPhraseOnly_ReturnsUpdatedEvent()
    {
        var repo = new FakeCertificateRepository();
        var cert = SeedCert(repo);
        var handler = new UpdateCertificateHandler(repo);

        var command = new UpdateCertificateCommand(cert.Id, null, "new-pass", "actor-1");
        var (_, @event) = await handler.Handle(command, CancellationToken.None);

        Assert.IsType<CertificateUpdatedEvent>(@event);
        Assert.Equal(cert.Id, @event.Id);
    }

    [Fact]
    public async Task Handle_EmptyName_ThrowsValidationException()
    {
        var repo = new FakeCertificateRepository();
        var cert = SeedCert(repo);
        var handler = new UpdateCertificateHandler(repo);

        var command = new UpdateCertificateCommand(cert.Id, "", null, "actor-1");

        await Assert.ThrowsAsync<CertificateValidationException>(() =>
            handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFoundException()
    {
        var repo = new FakeCertificateRepository();
        var handler = new UpdateCertificateHandler(repo);

        var command = new UpdateCertificateCommand(Guid.NewGuid(), "name", null, "actor-1");

        await Assert.ThrowsAsync<CertificateNotFoundException>(() =>
            handler.Handle(command, CancellationToken.None));
    }
}
