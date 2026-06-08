using West94.ProxyManager.API.Handlers;
using West94.ProxyManager.API.Tests.Unit.Fakes;
using West94.ProxyManager.Core.AggregatesModel.UserAggregate;
using West94.ProxyManager.Core.Messages.Queries;

namespace West94.ProxyManager.API.Tests.Unit.Handlers;

public class GetUserAuditLogHandlerTests
{
    private static UserAuditEntry MakeEntry(string sub = "sub|1", UserOperation op = UserOperation.Created) =>
        UserAuditEntry.Create(sub, op, null, UserAccessLevel.ReadOnly, "actor|1");

    [Fact]
    public async Task Handle_ReturnsPagedResult()
    {
        var auditRepo = new FakeUserAuditRepository();
        auditRepo.Entries.Add(MakeEntry("sub|1", UserOperation.Created));
        auditRepo.Entries.Add(MakeEntry("sub|2", UserOperation.Updated));
        var handler = new GetUserAuditLogHandler(auditRepo);

        var result = await handler.Handle(new GetUserAuditLogQuery(), CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task Handle_FiltersBySubFilter()
    {
        var auditRepo = new FakeUserAuditRepository();
        auditRepo.Entries.Add(MakeEntry("sub|1"));
        auditRepo.Entries.Add(MakeEntry("sub|2"));
        var handler = new GetUserAuditLogHandler(auditRepo);

        var result = await handler.Handle(new GetUserAuditLogQuery(SubFilter: "sub|1"), CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("sub|1", result.Items[0].SubjectSub);
    }

    [Fact]
    public async Task Handle_FiltersByDateRange()
    {
        var auditRepo = new FakeUserAuditRepository();
        auditRepo.Entries.Add(MakeEntry("sub|1"));
        var handler = new GetUserAuditLogHandler(auditRepo);

        var future = DateTimeOffset.UtcNow.AddDays(1);
        var result = await handler.Handle(new GetUserAuditLogQuery(From: future), CancellationToken.None);

        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task Handle_EmptyRepo_ReturnsEmptyResult()
    {
        var handler = new GetUserAuditLogHandler(new FakeUserAuditRepository());

        var result = await handler.Handle(new GetUserAuditLogQuery(), CancellationToken.None);

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task Handle_MapsAllDtoFields()
    {
        var auditRepo = new FakeUserAuditRepository();
        auditRepo.Entries.Add(MakeEntry("sub|1", UserOperation.Updated));
        var handler = new GetUserAuditLogHandler(auditRepo);

        var result = await handler.Handle(new GetUserAuditLogQuery(), CancellationToken.None);

        var dto = result.Items[0];
        Assert.Equal("sub|1", dto.SubjectSub);
        Assert.Equal(UserOperation.Updated, dto.Operation);
        Assert.Equal("actor|1", dto.ActorSub);
        Assert.NotEqual(Guid.Empty, dto.Id);
    }
}
