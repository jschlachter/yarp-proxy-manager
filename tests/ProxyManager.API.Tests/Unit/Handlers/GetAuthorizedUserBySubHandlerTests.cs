using West94.ProxyManager.API.Handlers;
using West94.ProxyManager.API.Tests.Unit.Fakes;
using West94.ProxyManager.Core.AggregatesModel.UserAggregate;
using West94.ProxyManager.Core.Messages.Queries;

namespace West94.ProxyManager.API.Tests.Unit.Handlers;

public class GetAuthorizedUserBySubHandlerTests
{
    private static AuthorizedUser MakeUser(string sub = "sub|1") =>
        AuthorizedUser.Create(sub, "Alice", "Al", "alice@example.com", null, UserAccessLevel.ReadOnly, "actor");

    [Fact]
    public async Task Handle_KnownSub_ReturnsDto()
    {
        var repo = new FakeAuthorizedUserRepository();
        repo.Seed(MakeUser("sub|1"));
        var handler = new GetAuthorizedUserBySubHandler(repo);

        var dto = await handler.Handle(new GetAuthorizedUserBySubQuery("sub|1"), CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal("sub|1", dto.Sub);
        Assert.Equal("Alice", dto.DisplayName);
    }

    [Fact]
    public async Task Handle_UnknownSub_ReturnsNull()
    {
        var repo = new FakeAuthorizedUserRepository();
        var handler = new GetAuthorizedUserBySubHandler(repo);

        var dto = await handler.Handle(new GetAuthorizedUserBySubQuery("unknown"), CancellationToken.None);

        Assert.Null(dto);
    }
}
