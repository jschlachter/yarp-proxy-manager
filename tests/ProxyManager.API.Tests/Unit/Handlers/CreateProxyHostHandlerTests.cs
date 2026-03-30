using West94.ProxyManager.API.Handlers;
using West94.ProxyManager.API.Tests.Unit.Fakes;
using West94.ProxyManager.Core.AggregatesModel.AuditLogAggregate;
using West94.ProxyManager.Core.AggregatesModel.ProxyHostAggregate;
using West94.ProxyManager.Core.Exceptions;
using West94.ProxyManager.Core.Messages.Commands;
using West94.ProxyManager.Core.Messages.Events;

namespace West94.ProxyManager.API.Tests.Unit.Handlers;

public class CreateProxyHostHandlerTests
{
    private static CreateProxyHostCommand ValidCommand(string domain = "new.example.com") => new(
        DomainNames: [domain],
        DestinationUri: "https://backend:8443",
        CertificatePath: null,
        CertificateKeyPath: null,
        ActorId: "user-123");

    [Fact]
    public async Task Handle_ValidCommand_ReturnsDtoWithNewId()
    {
        var repo = new FakeProxyHostRepository();
        var auditLog = new FakeAuditLogRepository();
        var handler = new CreateProxyHostHandler(repo, auditLog);

        var (dto, _) = await handler.Handle(ValidCommand(), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, dto.Id);
        Assert.Contains("new.example.com", dto.DomainNames);
        Assert.Equal("https://backend:8443", dto.Destination);
        Assert.True(dto.IsEnabled);
    }

    [Fact]
    public async Task Handle_ValidCommand_AddsHostToRepository()
    {
        var repo = new FakeProxyHostRepository();
        var auditLog = new FakeAuditLogRepository();
        var handler = new CreateProxyHostHandler(repo, auditLog);

        var (dto, _) = await handler.Handle(ValidCommand("added.example.com"), CancellationToken.None);

        var stored = await repo.FindAsync(dto.Id);
        Assert.NotNull(stored);
    }

    [Fact]
    public async Task Handle_DuplicateDomainName_ThrowsConflictException()
    {
        var repo = new FakeProxyHostRepository();
        repo.Seed(ProxyHost.Create(["existing.example.com"], DestinationUri.Parse("http://backend:8080")));
        var auditLog = new FakeAuditLogRepository();
        var handler = new CreateProxyHostHandler(repo, auditLog);

        await Assert.ThrowsAsync<ProxyHostConflictException>(() =>
            handler.Handle(ValidCommand("existing.example.com"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_InvalidDestinationUri_ThrowsValidationException()
    {
        var repo = new FakeProxyHostRepository();
        var auditLog = new FakeAuditLogRepository();
        var handler = new CreateProxyHostHandler(repo, auditLog);

        var command = ValidCommand() with { DestinationUri = "not-a-uri" };

        await Assert.ThrowsAsync<ProxyHostValidationException>(() =>
            handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_EmptyDomainNames_ThrowsValidationException()
    {
        var repo = new FakeProxyHostRepository();
        var auditLog = new FakeAuditLogRepository();
        var handler = new CreateProxyHostHandler(repo, auditLog);

        var command = ValidCommand() with { DomainNames = [] };

        await Assert.ThrowsAsync<ProxyHostValidationException>(() =>
            handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ValidCommand_AppendsAuditEntryWithCreatedOperationAndCorrectActor()
    {
        var repo = new FakeProxyHostRepository();
        var auditLog = new FakeAuditLogRepository();
        var handler = new CreateProxyHostHandler(repo, auditLog);

        await handler.Handle(ValidCommand(), CancellationToken.None);

        Assert.Single(auditLog.Entries);
        var entry = auditLog.Entries[0];
        Assert.Equal(AuditOperation.Created, entry.Operation);
        Assert.Equal("user-123", entry.ActorId);
        Assert.Null(entry.PreviousState);
    }

    [Fact]
    public async Task Handle_ValidCommand_ReturnsProxyHostCreatedEvent()
    {
        var repo = new FakeProxyHostRepository();
        var auditLog = new FakeAuditLogRepository();
        var handler = new CreateProxyHostHandler(repo, auditLog);

        var (dto, @event) = await handler.Handle(ValidCommand(), CancellationToken.None);

        Assert.NotNull(@event);
        Assert.IsType<ProxyHostCreatedEvent>(@event);
        Assert.Equal(dto.Id, @event.Id);
        Assert.Contains("new.example.com", @event.DomainNames);
    }
}
