using West94.ProxyManager.API.Handlers;
using West94.ProxyManager.API.Tests.Unit.Fakes;
using West94.ProxyManager.Core.Exceptions;
using West94.ProxyManager.Core.Messages.Commands;
using West94.ProxyManager.Core.Messages.Events;

namespace West94.ProxyManager.API.Tests.Unit.Handlers;

[Trait("Category", "Unit")]
public class CreateCertificateHandlerTests
{
    private static CreateCertificateCommand PfxCommand(string name = "my-pfx") => new(
        Name: name,
        Format: "Pfx",
        CertificatePath: "/certs/cert.pfx",
        KeyFilePath: null,
        PassPhrase: "secret",
        ActorId: "user-1");

    private static CreateCertificateCommand PemCommand(string name = "my-pem") => new(
        Name: name,
        Format: "Pem",
        CertificatePath: "/certs/cert.pem",
        KeyFilePath: "/certs/cert.key",
        PassPhrase: null,
        ActorId: "user-1");

    [Fact]
    public async Task Handle_ValidPfxCommand_ReturnsDtoWithNewId()
    {
        var repo = new FakeCertificateRepository();
        var handler = new CreateCertificateHandler(repo);

        var (dto, _) = await handler.Handle(PfxCommand(), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, dto.Id);
        Assert.Equal("my-pfx", dto.Name);
        Assert.Equal("Pfx", dto.Format);
        Assert.Equal("/certs/cert.pfx", dto.CertificatePath);
        Assert.Null(dto.KeyFilePath);
    }

    [Fact]
    public async Task Handle_ValidPfxCommand_ReturnsCertificateCreatedEvent()
    {
        var repo = new FakeCertificateRepository();
        var handler = new CreateCertificateHandler(repo);

        var (dto, @event) = await handler.Handle(PfxCommand(), CancellationToken.None);

        Assert.IsType<CertificateCreatedEvent>(@event);
        Assert.Equal(dto.Id, @event.Id);
        Assert.Equal("Pfx", @event.Format);
    }

    [Fact]
    public async Task Handle_ValidPemCommand_PreservesKeyFilePath()
    {
        var repo = new FakeCertificateRepository();
        var handler = new CreateCertificateHandler(repo);

        var (dto, _) = await handler.Handle(PemCommand(), CancellationToken.None);

        Assert.Equal("Pem", dto.Format);
        Assert.Equal("/certs/cert.key", dto.KeyFilePath);
    }

    [Fact]
    public async Task Handle_PfxWithKeyFilePath_ThrowsValidationException()
    {
        var repo = new FakeCertificateRepository();
        var handler = new CreateCertificateHandler(repo);
        var command = PfxCommand() with { KeyFilePath = "/certs/cert.key" };

        await Assert.ThrowsAsync<CertificateValidationException>(() =>
            handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_InvalidFormat_ThrowsValidationException()
    {
        var repo = new FakeCertificateRepository();
        var handler = new CreateCertificateHandler(repo);
        var command = PfxCommand() with { Format = "DER" };

        await Assert.ThrowsAsync<CertificateValidationException>(() =>
            handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_EmptyName_ThrowsValidationException()
    {
        var repo = new FakeCertificateRepository();
        var handler = new CreateCertificateHandler(repo);
        var command = PfxCommand() with { Name = "" };

        await Assert.ThrowsAsync<CertificateValidationException>(() =>
            handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ValidCommand_AddsCertToRepository()
    {
        var repo = new FakeCertificateRepository();
        var handler = new CreateCertificateHandler(repo);

        var (dto, _) = await handler.Handle(PfxCommand(), CancellationToken.None);

        var stored = await repo.FindAsync(dto.Id);
        Assert.NotNull(stored);
        Assert.Equal("my-pfx", stored.Name);
    }
}
