using West94.ProxyManager.API.Handlers;
using West94.ProxyManager.API.Tests.Unit.Fakes;
using West94.ProxyManager.Core.AggregatesModel.UserAggregate;
using West94.ProxyManager.Core.Exceptions;
using West94.ProxyManager.Core.Messages.Commands;
using West94.ProxyManager.Endpoints;

namespace West94.ProxyManager.API.Tests.Unit.Handlers;

public class CreateAuthorizedUserHandlerTests
{
    private static CreateAuthorizedUserCommand MakeCommand(
        string sub = "sub|1",
        string displayName = "Alice",
        string nickname = "Al",
        string email = "alice@example.com",
        string? profileImageUrl = null,
        UserAccessLevel level = UserAccessLevel.ReadOnly,
        string actorSub = "actor|1") =>
        new(sub, displayName, nickname, email, profileImageUrl, level, actorSub);

    [Fact]
    public async Task Handle_NewUser_ReturnsDto_AndAppendsCreatedAudit()
    {
        var userRepo = new FakeAuthorizedUserRepository();
        var auditRepo = new FakeUserAuditRepository();
        var handler = new CreateAuthorizedUserHandler(userRepo, auditRepo);

        var result = await handler.Handle(MakeCommand(), CancellationToken.None);

        Assert.False(result.Reactivated);
        Assert.Equal("sub|1", result.Dto.Sub);
        Assert.Single(auditRepo.Entries);
        Assert.Equal(UserOperation.Created, auditRepo.Entries[0].Operation);
    }

    [Fact]
    public async Task Handle_DeactivatedUser_Reactivates_AndAppendsReactivatedAudit()
    {
        var userRepo = new FakeAuthorizedUserRepository();
        var existing = AuthorizedUser.Create("sub|1", "Alice", "Al", "alice@example.com", null, UserAccessLevel.ReadOnly, "system");
        existing.Deactivate();
        userRepo.Seed(existing);

        var auditRepo = new FakeUserAuditRepository();
        var handler = new CreateAuthorizedUserHandler(userRepo, auditRepo);

        var result = await handler.Handle(MakeCommand(level: UserAccessLevel.Admin), CancellationToken.None);

        Assert.True(result.Reactivated);
        Assert.Equal(UserAccessLevel.Admin, result.Dto.AccessLevel);
        Assert.Equal(UserStatus.Active, result.Dto.Status);
        Assert.Single(auditRepo.Entries);
        Assert.Equal(UserOperation.Reactivated, auditRepo.Entries[0].Operation);
    }

    [Fact]
    public async Task Handle_ActiveConflict_ThrowsUserConflictException()
    {
        var userRepo = new FakeAuthorizedUserRepository();
        userRepo.Seed(AuthorizedUser.Create("sub|1", "Alice", "Al", "alice@example.com", null, UserAccessLevel.ReadOnly, "system"));
        var auditRepo = new FakeUserAuditRepository();
        var handler = new CreateAuthorizedUserHandler(userRepo, auditRepo);

        await Assert.ThrowsAsync<UserConflictException>(() =>
            handler.Handle(MakeCommand(), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_BlankSub_ThrowsUserValidationException()
    {
        var handler = new CreateAuthorizedUserHandler(new FakeAuthorizedUserRepository(), new FakeUserAuditRepository());

        await Assert.ThrowsAsync<UserValidationException>(() =>
            handler.Handle(MakeCommand(sub: ""), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_InvalidProfileImageUrl_ThrowsUserValidationException()
    {
        var handler = new CreateAuthorizedUserHandler(new FakeAuthorizedUserRepository(), new FakeUserAuditRepository());

        await Assert.ThrowsAsync<UserValidationException>(() =>
            handler.Handle(MakeCommand(profileImageUrl: "not-a-url"), CancellationToken.None));
    }
}
