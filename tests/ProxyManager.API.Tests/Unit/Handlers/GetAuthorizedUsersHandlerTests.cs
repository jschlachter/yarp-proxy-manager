using West94.ProxyManager.API.Handlers;
using West94.ProxyManager.API.Tests.Unit.Fakes;
using West94.ProxyManager.Core.AggregatesModel.UserAggregate;
using West94.ProxyManager.Core.Messages.Queries;

namespace West94.ProxyManager.API.Tests.Unit.Handlers;

public class GetAuthorizedUsersHandlerTests
{
    private static AuthorizedUser MakeUser(
        string sub = "sub|1",
        string displayName = "Alice",
        UserAccessLevel level = UserAccessLevel.ReadOnly,
        bool deactivated = false)
    {
        var u = AuthorizedUser.Create(sub, displayName, "al", "alice@example.com", null, level, "actor");
        if (deactivated) u.Deactivate();
        return u;
    }

    [Fact]
    public async Task Handle_ActiveUsersOnly_ReturnsPagedResult()
    {
        var repo = new FakeAuthorizedUserRepository();
        repo.Seed(MakeUser("sub|1", "Alice"), MakeUser("sub|2", "Bob"));
        var handler = new GetAuthorizedUsersHandler(repo);

        var result = await handler.Handle(new GetAuthorizedUsersQuery(), CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
    }

    [Fact]
    public async Task Handle_ExcludesDeactivatedByDefault()
    {
        var repo = new FakeAuthorizedUserRepository();
        repo.Seed(MakeUser("sub|1", "Alice"), MakeUser("sub|2", "Bob", deactivated: true));
        var handler = new GetAuthorizedUsersHandler(repo);

        var result = await handler.Handle(new GetAuthorizedUsersQuery(IncludeDeactivated: false), CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("sub|1", result.Items[0].Sub);
    }

    [Fact]
    public async Task Handle_IncludesDeactivatedWhenFlagSet()
    {
        var repo = new FakeAuthorizedUserRepository();
        repo.Seed(MakeUser("sub|1", "Alice"), MakeUser("sub|2", "Bob", deactivated: true));
        var handler = new GetAuthorizedUsersHandler(repo);

        var result = await handler.Handle(new GetAuthorizedUsersQuery(IncludeDeactivated: true), CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
    }

    [Fact]
    public async Task Handle_MapsAllDtoFields()
    {
        var repo = new FakeAuthorizedUserRepository();
        var user = AuthorizedUser.Create("sub|99", "Charlie", "Chas", "charlie@example.com", "https://img.example.com/c.png", UserAccessLevel.Admin, "actor");
        repo.Seed(user);
        var handler = new GetAuthorizedUsersHandler(repo);

        var result = await handler.Handle(new GetAuthorizedUsersQuery(), CancellationToken.None);

        var dto = result.Items[0];
        Assert.Equal("sub|99", dto.Sub);
        Assert.Equal("Charlie", dto.DisplayName);
        Assert.Equal("Chas", dto.Nickname);
        Assert.Equal("charlie@example.com", dto.Email);
        Assert.Equal("https://img.example.com/c.png", dto.ProfileImageUrl);
        Assert.Equal(UserAccessLevel.Admin, dto.AccessLevel);
        Assert.Equal(UserStatus.Active, dto.Status);
        Assert.Null(dto.DeactivatedAt);
    }
}
