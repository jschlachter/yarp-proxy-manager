using West94.ProxyManager.API.Handlers;
using West94.ProxyManager.API.Tests.Unit.Fakes;
using West94.ProxyManager.Core.AggregatesModel.UserAggregate;
using West94.ProxyManager.Core.Exceptions;
using West94.ProxyManager.Core.Messages.Commands;

namespace West94.ProxyManager.API.Tests.Unit.Handlers;

public class DeactivateUserHandlerTests
{
    private static AuthorizedUser MakeUser(string sub = "sub|1", bool deactivated = false)
    {
        var u = AuthorizedUser.Create(sub, "Alice", "Al", "alice@example.com", null, UserAccessLevel.ReadOnly, "system");
        if (deactivated) u.Deactivate();
        return u;
    }

    [Fact]
    public async Task Handle_ActiveUser_SetsDeactivatedStatus_AndAppendsAudit()
    {
        var userRepo = new FakeAuthorizedUserRepository();
        var user = MakeUser();
        userRepo.Seed(user);
        var auditRepo = new FakeUserAuditRepository();
        var handler = new DeactivateUserHandler(userRepo, auditRepo);

        await handler.Handle(new DeactivateUserCommand("sub|1", "actor|1"), CancellationToken.None);

        Assert.Equal(UserStatus.Deactivated, user.Status);
        Assert.NotNull(user.DeactivatedAt);
        Assert.Single(auditRepo.Entries);
        Assert.Equal(UserOperation.Deactivated, auditRepo.Entries[0].Operation);
        Assert.Equal(UserAccessLevel.ReadOnly, auditRepo.Entries[0].PreviousAccessLevel);
    }

    [Fact]
    public async Task Handle_UnknownUser_ThrowsUserNotFoundException()
    {
        var handler = new DeactivateUserHandler(new FakeAuthorizedUserRepository(), new FakeUserAuditRepository());

        await Assert.ThrowsAsync<UserNotFoundException>(() =>
            handler.Handle(new DeactivateUserCommand("nonexistent", "actor"), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_AlreadyDeactivatedUser_ThrowsUserNotFoundException()
    {
        var userRepo = new FakeAuthorizedUserRepository();
        userRepo.Seed(MakeUser(deactivated: true));
        var handler = new DeactivateUserHandler(userRepo, new FakeUserAuditRepository());

        await Assert.ThrowsAsync<UserNotFoundException>(() =>
            handler.Handle(new DeactivateUserCommand("sub|1", "actor"), CancellationToken.None));
    }
}
