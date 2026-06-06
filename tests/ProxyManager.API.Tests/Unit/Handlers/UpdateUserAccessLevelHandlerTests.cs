using West94.ProxyManager.API.Handlers;
using West94.ProxyManager.API.Tests.Unit.Fakes;
using West94.ProxyManager.Core.AggregatesModel.UserAggregate;
using West94.ProxyManager.Core.Exceptions;
using West94.ProxyManager.Core.Messages.Commands;

namespace West94.ProxyManager.API.Tests.Unit.Handlers;

public class UpdateUserAccessLevelHandlerTests
{
    private static AuthorizedUser MakeActiveUser(string sub = "sub|1", UserAccessLevel level = UserAccessLevel.ReadOnly)
    {
        var u = AuthorizedUser.Create(sub, "Alice", "Al", "alice@example.com", null, level, "system");
        return u;
    }

    [Fact]
    public async Task Handle_KnownActiveUser_UpdatesAccessLevel_AndAppendsAudit()
    {
        var userRepo = new FakeAuthorizedUserRepository();
        var user = MakeActiveUser(level: UserAccessLevel.ReadOnly);
        userRepo.Seed(user);
        var auditRepo = new FakeUserAuditRepository();
        var handler = new UpdateUserAccessLevelHandler(userRepo, auditRepo);

        var dto = await handler.Handle(new UpdateUserAccessLevelCommand("sub|1", UserAccessLevel.Admin, "actor|1"), CancellationToken.None);

        Assert.Equal(UserAccessLevel.Admin, dto.AccessLevel);
        Assert.Single(auditRepo.Entries);
        Assert.Equal(UserOperation.Updated, auditRepo.Entries[0].Operation);
        Assert.Equal(UserAccessLevel.ReadOnly, auditRepo.Entries[0].PreviousAccessLevel);
        Assert.Equal(UserAccessLevel.Admin, auditRepo.Entries[0].NewAccessLevel);
    }

    [Fact]
    public async Task Handle_UpdatesLastModifiedAt()
    {
        var userRepo = new FakeAuthorizedUserRepository();
        var user = MakeActiveUser();
        var originalModified = user.LastModifiedAt;
        userRepo.Seed(user);
        var handler = new UpdateUserAccessLevelHandler(userRepo, new FakeUserAuditRepository());

        var dto = await handler.Handle(new UpdateUserAccessLevelCommand("sub|1", UserAccessLevel.Admin, "actor"), CancellationToken.None);

        Assert.True(dto.LastModifiedAt >= originalModified);
    }

    [Fact]
    public async Task Handle_UnknownSub_ThrowsUserNotFoundException()
    {
        var handler = new UpdateUserAccessLevelHandler(new FakeAuthorizedUserRepository(), new FakeUserAuditRepository());

        await Assert.ThrowsAsync<UserNotFoundException>(() =>
            handler.Handle(new UpdateUserAccessLevelCommand("nonexistent", UserAccessLevel.Admin, "actor"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_SameAccessLevel_StillWritesAuditEntry()
    {
        var userRepo = new FakeAuthorizedUserRepository();
        userRepo.Seed(MakeActiveUser(level: UserAccessLevel.ReadOnly));
        var auditRepo = new FakeUserAuditRepository();
        var handler = new UpdateUserAccessLevelHandler(userRepo, auditRepo);

        await handler.Handle(new UpdateUserAccessLevelCommand("sub|1", UserAccessLevel.ReadOnly, "actor"), CancellationToken.None);

        Assert.Single(auditRepo.Entries);
    }
}
