extern alias ProxyManagerApp;
using ProxyManagerApp::West94.ProxyManager.Yarp;
using West94.ProxyManager.Core.AggregatesModel.ProxyHostAggregate;

namespace West94.ProxyManager.API.Tests.Unit.Yarp;

[Trait("Category", "Unit")]
public class ProxyHostYarpTranslatorTests
{
    private static ProxyHost MakeHost(string domain = "test.example.com", string destination = "http://backend:8080")
        => ProxyHost.Create([domain], DestinationUri.Parse(destination));

    [Fact]
    public void Translate_EnabledHost_ReturnsOneRouteAndOneCluster()
    {
        var host = MakeHost("app.example.com");

        var (routes, clusters) = ProxyHostYarpTranslator.Translate([host]);

        Assert.Single(routes);
        Assert.Single(clusters);
        Assert.Equal(host.Id.ToString(), routes[0].RouteId);
        Assert.Equal(host.Id.ToString(), clusters[0].ClusterId);
    }

    [Fact]
    public void Translate_DisabledHost_ExcludesFromResult()
    {
        var host = MakeHost();
        host.Disable();

        var (routes, clusters) = ProxyHostYarpTranslator.Translate([host]);

        Assert.Empty(routes);
        Assert.Empty(clusters);
    }

    [Fact]
    public void Translate_EmptyList_ReturnsEmptyResult()
    {
        var (routes, clusters) = ProxyHostYarpTranslator.Translate([]);

        Assert.Empty(routes);
        Assert.Empty(clusters);
    }

    [Fact]
    public void Translate_DomainNames_MappedToRouteMatchHosts()
    {
        var host = ProxyHost.Create(["one.example.com", "two.example.com"],
            DestinationUri.Parse("http://backend:8080"));

        var (routes, _) = ProxyHostYarpTranslator.Translate([host]);

        var matchHosts = routes[0].Match.Hosts;
        Assert.NotNull(matchHosts);
        Assert.Contains("one.example.com", matchHosts);
        Assert.Contains("two.example.com", matchHosts);
    }

    [Fact]
    public void Translate_Destination_MappedToClusterPrimaryAddress()
    {
        var host = MakeHost(destination: "http://backend:8080");

        var (_, clusters) = ProxyHostYarpTranslator.Translate([host]);

        Assert.True(clusters[0].Destinations!.ContainsKey("primary"));
        Assert.Equal("http://backend:8080", clusters[0].Destinations!["primary"].Address);
    }

    [Fact]
    public void Translate_Route_HasCatchAllPathAndOrder100()
    {
        var host = MakeHost();

        var (routes, _) = ProxyHostYarpTranslator.Translate([host]);

        Assert.Equal("/{**catch-all}", routes[0].Match.Path);
        Assert.Equal(100, routes[0].Order);
    }

    [Fact]
    public void Translate_MixedEnabledAndDisabled_OnlyIncludesEnabled()
    {
        var enabled = MakeHost("enabled.example.com");
        var disabled = MakeHost("disabled.example.com");
        disabled.Disable();

        var (routes, clusters) = ProxyHostYarpTranslator.Translate([enabled, disabled]);

        Assert.Single(routes);
        Assert.Single(clusters);
        Assert.Equal(enabled.Id.ToString(), routes[0].RouteId);
    }
}
