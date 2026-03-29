using West94.ProxyManager.API.Handlers;
using West94.ProxyManager.API.Tests.Unit.Fakes;
using West94.ProxyManager.Core.AggregatesModel.AuditLogAggregate;
using West94.ProxyManager.Core.AggregatesModel.ProxyHostAggregate;
using West94.ProxyManager.Core.Exceptions;
using West94.ProxyManager.Core.Messages.Commands;
using West94.ProxyManager.Core.Messages.Events;

namespace West94.ProxyManager.API.Tests.Unit.Handlers;

public class DeleteProxyHostHandlerTests
{
    private static ProxyHost SeedHost(FakeProxyHostRepository repo, string domain = "delete-test.example.com")
    {
        var host = ProxyHost.Create([domain], DestinationUri.Parse("http://backend:8080"));
        repo.Seed(host);
        return host;
    }

    [Fact]
    public async Task Handle_ExistingId_CallsRemoveAsync()
    {
        var repo = new FakeProxyHostRepository();
        var host = SeedHost(repo);
        var auditLog = new FakeAuditLogRepository();
        var handler = new DeleteProxyHostHandler(repo, auditLog);

        await handler.Handle(new DeleteProxyHostCommand(host.Id, "actor-1"), CancellationToken.None);

        var remaining = await repo.FindAsync(host.Id);
        Assert.Null(remaining);
    }

    [Fact]
    public async Task Handle_UnknownId_ThrowsNotFoundException()
    {
        var repo = new FakeProxyHostRepository();
        var auditLog = new FakeAuditLogRepository();
        var handler = new DeleteProxyHostHandler(repo, auditLog);

        await Assert.ThrowsAsync<ProxyHostNotFoundException>(() =>
            handler.Handle(new DeleteProxyHostCommand(Guid.NewGuid(), "actor-1"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ExistingId_AppendsAuditEntryWithDeletedOperationAndPreviousState()
    {
        var repo = new FakeProxyHostRepository();
        var host = SeedHost(repo);
        var auditLog = new FakeAuditLogRepository();
        var handler = new DeleteProxyHostHandler(repo, auditLog);

        await handler.Handle(new DeleteProxyHostCommand(host.Id, "actor-99"), CancellationToken.None);

        Assert.Single(auditLog.Entries);
        var entry = auditLog.Entries[0];
        Assert.Equal(AuditOperation.Deleted, entry.Operation);
        Assert.Equal("actor-99", entry.ActorId);
        Assert.Equal(host.Id, entry.ProxyHostId);
        Assert.NotNull(entry.PreviousState);
        Assert.Null(entry.NewState);
    }

    [Fact]
    public async Task Handle_ExistingId_ReturnsProxyHostDeletedEventWithCorrectDomainNames()
    {
        var repo = new FakeProxyHostRepository();
        var host = SeedHost(repo, "deleted-domain.example.com");
        var auditLog = new FakeAuditLogRepository();
        var handler = new DeleteProxyHostHandler(repo, auditLog);

        var @event = await handler.Handle(new DeleteProxyHostCommand(host.Id, "actor-1"), CancellationToken.None);

        Assert.NotNull(@event);
        Assert.IsType<ProxyHostDeletedEvent>(@event);
        Assert.Equal(host.Id, @event.Id);
        Assert.Contains("deleted-domain.example.com", @event.DomainNames);
    }
}
