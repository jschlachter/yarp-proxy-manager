using Microsoft.Extensions.Logging.Abstractions;
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
    private static DeleteCertificateHandler MakeHandler(FakeCertificateRepository repo) =>
        new(repo, NullLogger<DeleteCertificateHandler>.Instance);

    [Fact]
    public async Task Handle_ExistingId_ReturnsCertificateDeletedEvent()
    {
        var repo = new FakeCertificateRepository();
        var cert = Certificate.Create("test", CertificateFormat.Pem, "/certs/cert.pem");
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
        var cert = Certificate.Create("test", CertificateFormat.Pfx, "/certs/cert.pfx");
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

    [Fact]
    public async Task Handle_ExistingCertWithTempFiles_DeletesFilesFromDisk()
    {
        var certFile = Path.GetTempFileName();
        var keyFile = Path.GetTempFileName();

        var repo = new FakeCertificateRepository();
        var cert = Certificate.Create("disk-test", CertificateFormat.Pem, certFile, keyFile);
        repo.Seed(cert);
        var handler = MakeHandler(repo);

        await handler.Handle(new DeleteCertificateCommand(cert.Id, "actor-1"), CancellationToken.None);

        Assert.False(File.Exists(certFile));
        Assert.False(File.Exists(keyFile));
    }

    [Fact]
    public async Task Handle_MissingFilesOnDisk_DoesNotThrow()
    {
        var repo = new FakeCertificateRepository();
        var cert = Certificate.Create("missing-files", CertificateFormat.Pfx, "/nonexistent/cert.pfx");
        repo.Seed(cert);
        var handler = MakeHandler(repo);

        // should complete without throwing even though files don't exist
        var @event = await handler.Handle(new DeleteCertificateCommand(cert.Id, "actor-1"), CancellationToken.None);
        Assert.NotNull(@event);
    }
}
