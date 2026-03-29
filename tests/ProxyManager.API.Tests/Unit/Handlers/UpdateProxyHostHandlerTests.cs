using West94.ProxyManager.API.Handlers;
using West94.ProxyManager.API.Tests.Unit.Fakes;
using West94.ProxyManager.Core.AggregatesModel.AuditLogAggregate;
using West94.ProxyManager.Core.AggregatesModel.ProxyHostAggregate;
using West94.ProxyManager.Core.Exceptions;
using West94.ProxyManager.Core.Messages.Commands;
using West94.ProxyManager.Core.Messages.Events;

namespace West94.ProxyManager.API.Tests.Unit.Handlers;

public class UpdateProxyHostHandlerTests
{
    private static ProxyHost SeedHost(FakeProxyHostRepository repo, string domain = "update-test.example.com")
    {
        var host = ProxyHost.Create([domain], DestinationUri.Parse("http://original:8080"));
        repo.Seed(host);
        return host;
    }

    [Fact]
    public async Task Handle_PartialUpdate_OnlyIsEnabled_LeavesOtherFieldsUnchanged()
    {
        var repo = new FakeProxyHostRepository();
        var host = SeedHost(repo);
        var auditLog = new FakeAuditLogRepository();
        var handler = new UpdateProxyHostHandler(repo, auditLog);

        var command = new UpdateProxyHostCommand(host.Id, null, null, false, null, null, "actor-1");

        var (dto, _) = await handler.Handle(command, CancellationToken.None);

        Assert.Equal(host.Id, dto.Id);
        Assert.False(dto.IsEnabled);
        Assert.Equal("http://original:8080", dto.Destination);
        Assert.Contains("update-test.example.com", dto.DomainNames);
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFoundException()
    {
        var repo = new FakeProxyHostRepository();
        var auditLog = new FakeAuditLogRepository();
        var handler = new UpdateProxyHostHandler(repo, auditLog);

        var command = new UpdateProxyHostCommand(Guid.NewGuid(), null, null, false, null, null, "actor-1");

        await Assert.ThrowsAsync<ProxyHostNotFoundException>(() =>
            handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_InvalidDestinationUri_ThrowsValidationException()
    {
        var repo = new FakeProxyHostRepository();
        var host = SeedHost(repo);
        var auditLog = new FakeAuditLogRepository();
        var handler = new UpdateProxyHostHandler(repo, auditLog);

        var command = new UpdateProxyHostCommand(host.Id, null, "not-a-uri", null, null, null, "actor-1");

        await Assert.ThrowsAsync<ProxyHostValidationException>(() =>
            handler.Handle(command, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ValidUpdate_AppendsAuditEntryWithUpdatedOperationAndBothSnapshots()
    {
        var repo = new FakeProxyHostRepository();
        var host = SeedHost(repo);
        var auditLog = new FakeAuditLogRepository();
        var handler = new UpdateProxyHostHandler(repo, auditLog);

        var command = new UpdateProxyHostCommand(host.Id, null, null, false, null, null, "actor-99");

        await handler.Handle(command, CancellationToken.None);

        Assert.Single(auditLog.Entries);
        var entry = auditLog.Entries[0];
        Assert.Equal(AuditOperation.Updated, entry.Operation);
        Assert.Equal("actor-99", entry.ActorId);
        Assert.Equal(host.Id, entry.ProxyHostId);
        Assert.NotNull(entry.PreviousState);
        Assert.NotNull(entry.NewState);
    }

    [Fact]
    public async Task Handle_ValidUpdate_ReturnsProxyHostUpdatedEvent()
    {
        var repo = new FakeProxyHostRepository();
        var host = SeedHost(repo);
        var auditLog = new FakeAuditLogRepository();
        var handler = new UpdateProxyHostHandler(repo, auditLog);

        var command = new UpdateProxyHostCommand(host.Id, null, null, false, null, null, "actor-1");

        var (dto, @event) = await handler.Handle(command, CancellationToken.None);

        Assert.NotNull(@event);
        Assert.IsType<ProxyHostUpdatedEvent>(@event);
        Assert.Equal(dto.Id, @event.Id);
        Assert.False(@event.IsEnabled);
    }

    [Fact]
    public async Task Handle_UpdateDestination_ChangesDestinationInResult()
    {
        var repo = new FakeProxyHostRepository();
        var host = SeedHost(repo);
        var auditLog = new FakeAuditLogRepository();
        var handler = new UpdateProxyHostHandler(repo, auditLog);

        var command = new UpdateProxyHostCommand(host.Id, null, "https://new-backend:9000", null, null, null, "actor-1");

        var (dto, _) = await handler.Handle(command, CancellationToken.None);

        Assert.Equal("https://new-backend:9000", dto.Destination);
    }
}
