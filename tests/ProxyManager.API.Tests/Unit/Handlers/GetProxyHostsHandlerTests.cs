using West94.ProxyManager.API.Handlers;
using West94.ProxyManager.API.Tests.Unit.Fakes;
using West94.ProxyManager.Core.AggregatesModel.ProxyHostAggregate;
using West94.ProxyManager.Core.Messages.Queries;

namespace West94.ProxyManager.API.Tests.Unit.Handlers;

public class GetProxyHostsHandlerTests
{
    private static ProxyHost MakeHost(string firstDomain, string destination = "http://localhost:8080") =>
        ProxyHost.Create([firstDomain], DestinationUri.Parse(destination));

    [Fact]
    public async Task Handle_EmptyRepository_ReturnsEmptyPagedResult()
    {
        var repo = new FakeProxyHostRepository();
        var handler = new GetProxyHostsHandler(repo);

        var result = await handler.Handle(new GetProxyHostsQuery(), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
    }

    [Fact]
    public async Task Handle_RepositoryWithItems_ReturnsSortedByFirstDomainName()
    {
        var repo = new FakeProxyHostRepository();
        repo.Seed(
            MakeHost("zebra.example.com"),
            MakeHost("alpha.example.com"),
            MakeHost("mango.example.com"));
        var handler = new GetProxyHostsHandler(repo);

        var result = await handler.Handle(new GetProxyHostsQuery(), CancellationToken.None);

        Assert.Equal(3, result.TotalCount);
        Assert.Equal("alpha.example.com", result.Items[0].DomainNames[0]);
        Assert.Equal("mango.example.com", result.Items[1].DomainNames[0]);
        Assert.Equal("zebra.example.com", result.Items[2].DomainNames[0]);
    }

    [Fact]
    public async Task Handle_PageTwoWithPageSizeTwo_ReturnsCorrectSlice()
    {
        var repo = new FakeProxyHostRepository();
        repo.Seed(
            MakeHost("a.example.com"),
            MakeHost("b.example.com"),
            MakeHost("c.example.com"),
            MakeHost("d.example.com"),
            MakeHost("e.example.com"));
        var handler = new GetProxyHostsHandler(repo);

        var result = await handler.Handle(new GetProxyHostsQuery(Page: 2, PageSize: 2), CancellationToken.None);

        Assert.Equal(5, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.Equal("c.example.com", result.Items[0].DomainNames[0]);
        Assert.Equal("d.example.com", result.Items[1].DomainNames[0]);
    }
}
