using West94.ProxyManager.API.Handlers;
using West94.ProxyManager.API.Tests.Unit.Fakes;
using West94.ProxyManager.Core.AggregatesModel.AuditLogAggregate;
using West94.ProxyManager.Core.AggregatesModel.ProxyHostAggregate;
using West94.ProxyManager.Core.DTOs;
using West94.ProxyManager.Core.Exceptions;
using West94.ProxyManager.Core.Messages.Commands;
using West94.ProxyManager.Core.Messages.Events;

namespace West94.ProxyManager.API.Tests.Unit.Handlers;

[Trait("Category", "Unit")]
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

        var command = new UpdateProxyHostCommand(host.Id, null, null, false, "actor-1");

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

        var command = new UpdateProxyHostCommand(Guid.NewGuid(), null, null, false, "actor-1");

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

        var command = new UpdateProxyHostCommand(host.Id, null, "not-a-uri", null, "actor-1");

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

        var command = new UpdateProxyHostCommand(host.Id, null, null, false, "actor-99");

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

        var command = new UpdateProxyHostCommand(host.Id, null, null, false, "actor-1");

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

        var command = new UpdateProxyHostCommand(host.Id, null, "https://new-backend:9000", null, "actor-1");

        var (dto, _) = await handler.Handle(command, CancellationToken.None);

        Assert.Equal("https://new-backend:9000", dto.Destination);
    }

    [Fact]
    public async Task Handle_WithTlsMode_UpdatesTlsModeInResult()
    {
        var repo = new FakeProxyHostRepository();
        var host = SeedHost(repo);
        var auditLog = new FakeAuditLogRepository();
        var handler = new UpdateProxyHostHandler(repo, auditLog);

        var command = new UpdateProxyHostCommand(host.Id, null, null, null, "actor-1", "LetsEncrypt");

        var (dto, _) = await handler.Handle(command, CancellationToken.None);

        Assert.Equal("LetsEncrypt", dto.TlsMode);
    }

    [Fact]
    public async Task Handle_WithoutTlsMode_LeavesTlsModeUnchanged()
    {
        var repo = new FakeProxyHostRepository();
        var host = SeedHost(repo);
        var auditLog = new FakeAuditLogRepository();
        var handler = new UpdateProxyHostHandler(repo, auditLog);

        var command = new UpdateProxyHostCommand(host.Id, null, null, false, "actor-1");

        var (dto, _) = await handler.Handle(command, CancellationToken.None);

        Assert.Equal("Manual", dto.TlsMode);
    }

    [Fact]
    public async Task Handle_WithInvalidTlsMode_ThrowsValidationException()
    {
        var repo = new FakeProxyHostRepository();
        var host = SeedHost(repo);
        var auditLog = new FakeAuditLogRepository();
        var handler = new UpdateProxyHostHandler(repo, auditLog);

        var command = new UpdateProxyHostCommand(host.Id, null, null, null, "actor-1", "NotARealMode");

        await Assert.ThrowsAsync<ProxyHostValidationException>(() =>
            handler.Handle(command, CancellationToken.None));
    }

    private static ProxyHost SeedHostWithPassiveCheck(FakeProxyHostRepository repo)
    {
        var host = ProxyHost.Create(
            ["health-update.example.com"],
            DestinationUri.Parse("http://original:8080"),
            healthCheck: new HealthCheckSettings(
                null,
                new PassiveHealthCheck(PassiveHealthCheckPolicy.TransportFailureRate, failureRateLimit: 0.4),
                AvailableDestinationsPolicy.HealthyOrPanic));
        repo.Seed(host);
        return host;
    }

    [Fact]
    public async Task Handle_HealthCheckOmitted_LeavesSettingsUnchanged()
    {
        var repo = new FakeProxyHostRepository();
        var host = SeedHostWithPassiveCheck(repo);
        var original = host.HealthCheck;
        var handler = new UpdateProxyHostHandler(repo, new FakeAuditLogRepository());

        var (dto, _) = await handler.Handle(
            new UpdateProxyHostCommand(host.Id, null, null, false, "actor-1"), CancellationToken.None);

        Assert.Equal(original, (await repo.FindAsync(host.Id))!.HealthCheck);
        Assert.Equal(0.4, dto.HealthCheck!.Passive!.FailureRateLimit);
    }

    [Fact]
    public async Task Handle_HealthCheckWithBothChecksNull_ClearsSettings()
    {
        var repo = new FakeProxyHostRepository();
        var host = SeedHostWithPassiveCheck(repo);
        var handler = new UpdateProxyHostHandler(repo, new FakeAuditLogRepository());

        var command = new UpdateProxyHostCommand(host.Id, null, null, null, "actor-1",
            HealthCheck: new HealthCheckDto("HealthyAndUnknown", null, null));
        var (dto, _) = await handler.Handle(command, CancellationToken.None);

        Assert.Null(dto.HealthCheck);
        Assert.Null((await repo.FindAsync(host.Id))!.HealthCheck);
    }
}
