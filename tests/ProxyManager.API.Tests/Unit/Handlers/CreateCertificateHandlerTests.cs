using Microsoft.Extensions.Logging.Abstractions;

using West94.ProxyManager.API.Handlers;
using West94.ProxyManager.API.Tests.Unit.Fakes;
using West94.ProxyManager.Core.Exceptions;
using West94.ProxyManager.Core.Messages.Commands;
using West94.ProxyManager.Core.Messages.Events;

namespace West94.ProxyManager.API.Tests.Unit.Handlers;

[Trait("Category", "Unit")]
public class CreateCertificateHandlerTests
{
    private static CreateCertificateHandler MakeHandler(FakeCertificateRepository repo, FakeFileAssetClient files) =>
        new(repo, files, NullLogger<CreateCertificateHandler>.Instance);

    private static (FakeFileAssetClient Files, Guid CertAssetId, Guid KeyAssetId) SeedPemAssets()
    {
        var (certPem, keyPem) = TestCertificateGenerator.CreatePemPair();
        var files = new FakeFileAssetClient();
        var certAssetId = Guid.NewGuid();
        var keyAssetId = Guid.NewGuid();
        files.Seed(certAssetId, "cert.pem", certPem);
        files.Seed(keyAssetId, "cert.key", keyPem);
        return (files, certAssetId, keyAssetId);
    }

    private static (FakeFileAssetClient Files, Guid CertAssetId) SeedPfxAsset(string? passPhrase = null)
    {
        var pfxBytes = TestCertificateGenerator.CreatePfx(password: passPhrase);
        var files = new FakeFileAssetClient();
        var certAssetId = Guid.NewGuid();
        files.Seed(certAssetId, "bundle.pfx", pfxBytes);
        return (files, certAssetId);
    }

    [Fact]
    public async Task Handle_ValidPfxCommand_ReturnsDtoWithNewId()
    {
        var repo = new FakeCertificateRepository();
        var (files, certAssetId) = SeedPfxAsset("secret");
        var handler = MakeHandler(repo, files);
        var command = new CreateCertificateCommand("my-pfx", "Pfx", certAssetId, null, "secret", "user-1");

        var (dto, _) = await handler.Handle(command, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, dto.Id);
        Assert.Equal("my-pfx", dto.Name);
        Assert.Equal("Pfx", dto.Format);
        Assert.Equal(certAssetId, dto.CertificateAssetId);
        Assert.Null(dto.KeyAssetId);
    }

    [Fact]
    public async Task Handle_ValidPfxCommand_ReturnsCertificateCreatedEvent()
    {
        var repo = new FakeCertificateRepository();
        var (files, certAssetId) = SeedPfxAsset("secret");
        var handler = MakeHandler(repo, files);
        var command = new CreateCertificateCommand("my-pfx", "Pfx", certAssetId, null, "secret", "user-1");

        var (dto, @event) = await handler.Handle(command, CancellationToken.None);

        Assert.IsType<CertificateCreatedEvent>(@event);
        Assert.Equal(dto.Id, @event.Id);
        Assert.Equal("Pfx", @event.Format);
    }

    [Fact]
    public async Task Handle_ValidPemCommand_PreservesKeyAssetId()
    {
        var repo = new FakeCertificateRepository();
        var (files, certAssetId, keyAssetId) = SeedPemAssets();
        var handler = MakeHandler(repo, files);
        var command = new CreateCertificateCommand("my-pem", "Pem", certAssetId, keyAssetId, null, "user-1");

        var (dto, _) = await handler.Handle(command, CancellationToken.None);

        Assert.Equal("Pem", dto.Format);
        Assert.Equal(keyAssetId, dto.KeyAssetId);
    }

    [Fact]
    public async Task Handle_ValidPemCommand_CommitsBothAssets()
    {
        var repo = new FakeCertificateRepository();
        var (files, certAssetId, keyAssetId) = SeedPemAssets();
        var handler = MakeHandler(repo, files);
        var command = new CreateCertificateCommand("my-pem", "Pem", certAssetId, keyAssetId, null, "user-1");

        var (dto, _) = await handler.Handle(command, CancellationToken.None);

        Assert.Contains(files.Commits, c => c.Id == certAssetId && c.OwnerType == "certificate" && c.OwnerId == dto.Id);
        Assert.Contains(files.Commits, c => c.Id == keyAssetId && c.OwnerType == "certificate" && c.OwnerId == dto.Id);
    }

    [Fact]
    public async Task Handle_PfxWithKeyAssetId_ThrowsValidationException()
    {
        var repo = new FakeCertificateRepository();
        var (files, certAssetId, keyAssetId) = SeedPemAssets();
        var handler = MakeHandler(repo, files);
        var command = new CreateCertificateCommand("bad-pfx", "Pfx", certAssetId, keyAssetId, null, "user-1");

        await Assert.ThrowsAsync<CertificateValidationException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_InvalidFormat_ThrowsValidationException()
    {
        var repo = new FakeCertificateRepository();
        var (files, certAssetId) = SeedPfxAsset();
        var handler = MakeHandler(repo, files);
        var command = new CreateCertificateCommand("bad-format", "DER", certAssetId, null, null, "user-1");

        await Assert.ThrowsAsync<CertificateValidationException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_UnknownCertificateAssetId_ThrowsValidationException()
    {
        var repo = new FakeCertificateRepository();
        var files = new FakeFileAssetClient();
        var handler = MakeHandler(repo, files);
        var command = new CreateCertificateCommand("missing-asset", "Pem", Guid.NewGuid(), null, null, "user-1");

        await Assert.ThrowsAsync<CertificateValidationException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_WrongPfxPassphrase_ThrowsValidationException()
    {
        var repo = new FakeCertificateRepository();
        var (files, certAssetId) = SeedPfxAsset("correct-pass");
        var handler = MakeHandler(repo, files);
        var command = new CreateCertificateCommand("bad-pass", "Pfx", certAssetId, null, "wrong-pass", "user-1");

        await Assert.ThrowsAsync<CertificateValidationException>(() => handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ValidCommand_AddsCertToRepository()
    {
        var repo = new FakeCertificateRepository();
        var (files, certAssetId) = SeedPfxAsset("secret");
        var handler = MakeHandler(repo, files);
        var command = new CreateCertificateCommand("my-pfx", "Pfx", certAssetId, null, "secret", "user-1");

        var (dto, _) = await handler.Handle(command, CancellationToken.None);

        var stored = await repo.FindAsync(dto.Id);
        Assert.NotNull(stored);
        Assert.Equal("my-pfx", stored.Name);
    }
}
