extern alias ProxyManagerApp;

using ProxyManagerApp::West94.ProxyManager.Options;
using ProxyManagerApp::West94.ProxyManager.Yarp;
using West94.ProxyManager.Core.AggregatesModel.ProxyHostAggregate;
using Yarp.ReverseProxy.Configuration;

namespace West94.ProxyManager.API.Tests.Unit.Yarp;

[Trait("Category", "Unit")]
public class ManagementHostRouteFilterTests
{
    private static readonly ManagementHostRouteFilter Filter =
        new(Microsoft.Extensions.Options.Options.Create(new ManagementOptions { Hosts = ["manage.example.com"] }));

    [Fact]
    public async Task ConfigureRoute_SystemRouteWithoutHosts_IsScopedToManagementHosts()
    {
        var route = new RouteConfig
        {
            RouteId = "apiRoute",
            ClusterId = "apiCluster",
            Match = new RouteMatch { Path = "/api/{**catch-all}" }
        };

        var result = await Filter.ConfigureRouteAsync(route, null, TestContext.Current.CancellationToken);

        Assert.Equal(["manage.example.com"], result.Match.Hosts);
        Assert.Equal("/api/{**catch-all}", result.Match.Path);
    }

    [Fact]
    public async Task ConfigureRoute_UserRoute_KeepsItsOwnHosts()
    {
        var host = ProxyHost.Create(["app.example.com"], DestinationUri.Parse("http://backend:8080"));
        var route = ProxyHostYarpTranslator.Translate([host]).Routes[0];

        var result = await Filter.ConfigureRouteAsync(route, null, TestContext.Current.CancellationToken);

        Assert.Same(route, result);
        Assert.Equal(["app.example.com"], result.Match.Hosts);
    }
}
