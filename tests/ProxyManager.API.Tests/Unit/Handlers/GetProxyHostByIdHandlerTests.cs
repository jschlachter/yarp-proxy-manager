using West94.ProxyManager.API.Handlers;
using West94.ProxyManager.API.Tests.Unit.Fakes;
using West94.ProxyManager.Core.AggregatesModel.ProxyHostAggregate;
using West94.ProxyManager.Core.Messages.Queries;

namespace West94.ProxyManager.API.Tests.Unit.Handlers;

public class GetProxyHostByIdHandlerTests
{
    [Fact]
    public async Task Handle_ExistingId_ReturnsPopulatedDto()
    {
        var host = ProxyHost.Create(["example.com"], DestinationUri.Parse("https://backend:8443"));
        var repo = new FakeProxyHostRepository();
        repo.Seed(host);
        var handler = new GetProxyHostByIdHandler(repo);

        var result = await handler.Handle(new GetProxyHostByIdQuery(host.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(host.Id, result.Id);
        Assert.Contains("example.com", result.DomainNames);
        Assert.Equal("https://backend:8443", result.Destination);
        Assert.True(result.IsEnabled);
        Assert.Null(result.Certificate);
    }

    [Fact]
    public async Task Handle_UnknownId_ReturnsNull()
    {
        var repo = new FakeProxyHostRepository();
        var handler = new GetProxyHostByIdHandler(repo);

        var result = await handler.Handle(new GetProxyHostByIdQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Null(result);
    }
}
