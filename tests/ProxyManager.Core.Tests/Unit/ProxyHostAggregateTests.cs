using West94.ProxyManager.Core.AggregatesModel.ProxyHostAggregate;

namespace West94.ProxyManager.Core.Tests.Unit;

public class ProxyHostAggregateTests
{
    [Fact]
    public void Create_WithValidArgs_ReturnsEnabledHostWithNewId()
    {
        var host = ProxyHost.Create(["example.com"], DestinationUri.Parse("http://backend:8080"));

        Assert.NotEqual(Guid.Empty, host.Id);
        Assert.Single(host.DomainNames);
        Assert.Equal("example.com", host.DomainNames[0]);
        Assert.True(host.IsEnabled);
        Assert.Null(host.Certificate);
    }

    [Fact]
    public void Create_WithNullDomainNames_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ProxyHost.Create(null!, DestinationUri.Parse("http://backend:8080")));
    }

    [Fact]
    public void Create_WithEmptyDomainNames_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            ProxyHost.Create([], DestinationUri.Parse("http://backend:8080")));
    }

    [Fact]
    public void Create_WithNullDestination_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ProxyHost.Create(["example.com"], null!));
    }

    [Fact]
    public void Enable_AfterDisable_SetsIsEnabledTrue()
    {
        var host = ProxyHost.Create(["example.com"], DestinationUri.Parse("http://backend:8080"));
        host.Disable();

        host.Enable();

        Assert.True(host.IsEnabled);
    }

    [Fact]
    public void Disable_SetsIsEnabledFalse()
    {
        var host = ProxyHost.Create(["example.com"], DestinationUri.Parse("http://backend:8080"));

        host.Disable();

        Assert.False(host.IsEnabled);
    }

    [Fact]
    public void UpdateDestination_WithValidUri_UpdatesDestination()
    {
        var host = ProxyHost.Create(["example.com"], DestinationUri.Parse("http://old:8080"));

        host.UpdateDestination(DestinationUri.Parse("https://new:9090"));

        Assert.Equal("https://new:9090", host.Destination.ToString());
    }

    [Fact]
    public void UpdateDomainNames_WithEmptyList_ThrowsArgumentException()
    {
        var host = ProxyHost.Create(["example.com"], DestinationUri.Parse("http://backend:8080"));

        Assert.Throws<ArgumentException>(() => host.UpdateDomainNames([]));
    }
}
