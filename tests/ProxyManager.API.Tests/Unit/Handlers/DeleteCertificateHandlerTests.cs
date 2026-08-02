using West94.ProxyManager.API.Handlers;
using West94.ProxyManager.API.Tests.Unit.Fakes;
using West94.ProxyManager.Core.AggregatesModel.CertificateAggregate;
using West94.ProxyManager.Core.Exceptions;
using West94.ProxyManager.Core.Messages.Commands;
using West94.ProxyManager.Core.Messages.Events;

namespace West94.ProxyManager.API.Tests.Unit.Handlers;

[Trait("Category", "Unit")]
public class DeleteCertificateHandlerTests
{
    private static DeleteCertificateHandler MakeHandler(FakeCertificateRepository repo) => new(repo);

    [Fact]
    public async Task Handle_ExistingId_ReturnsCertificateDeletedEvent()
    {
        var repo = new FakeCertificateRepository();
        var cert = TestCertificates.Create("test", CertificateFormat.Pem);
        repo.Seed(cert);
        var handler = MakeHandler(repo);

        var @event = await handler.Handle(new DeleteCertificateCommand(cert.Id, "actor-1"), CancellationToken.None);

        Assert.IsType<CertificateDeletedEvent>(@event);
        Assert.Equal(cert.Id, @event.Id);
    }

    [Fact]
    public async Task Handle_ExistingId_RemovesCertFromRepository()
    {
        var repo = new FakeCertificateRepository();
        var cert = TestCertificates.Create("test", CertificateFormat.Pfx);
        repo.Seed(cert);
        var handler = MakeHandler(repo);

        await handler.Handle(new DeleteCertificateCommand(cert.Id, "actor-1"), CancellationToken.None);

        var stored = await repo.FindAsync(cert.Id);
        Assert.Null(stored);
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFoundException()
    {
        var repo = new FakeCertificateRepository();
        var handler = MakeHandler(repo);

        await Assert.ThrowsAsync<CertificateNotFoundException>(() =>
            handler.Handle(new DeleteCertificateCommand(Guid.NewGuid(), "actor-1"), CancellationToken.None));
    }
}
