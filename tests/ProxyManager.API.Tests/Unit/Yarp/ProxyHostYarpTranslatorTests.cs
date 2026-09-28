extern alias ProxyManagerApp;
using System.Globalization;

using Microsoft.Extensions.DependencyInjection;

using ProxyManagerApp::West94.ProxyManager.Yarp;
using West94.ProxyManager.Core.AggregatesModel.ProxyHostAggregate;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Health;

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

    private static ProxyHost MakeHostWithHealth(ActiveHealthCheck? active, PassiveHealthCheck? passive,
        AvailableDestinationsPolicy policy = AvailableDestinationsPolicy.HealthyAndUnknown) =>
        ProxyHost.Create(["health.example.com"], DestinationUri.Parse("http://backend:8080"),
            healthCheck: new HealthCheckSettings(active, passive, policy));

    private static readonly ActiveHealthCheck FullActive = new(
        ActiveHealthCheckPolicy.ConsecutiveFailures,
        TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(5), "/health", "?deep=1", "http://probe:9000/", 3);

    [Fact]
    public void Translate_NoHealthCheck_LeavesHealthAndMetadataNull()
    {
        var (_, clusters) = ProxyHostYarpTranslator.Translate([MakeHost()]);

        Assert.Null(clusters[0].HealthCheck);
        Assert.Null(clusters[0].Metadata);
        Assert.Null(clusters[0].Destinations!["primary"].Health);
    }

    [Fact]
    public void Translate_ActiveOnly_MapsEveryFieldAndThresholdMetadata()
    {
        var (_, clusters) = ProxyHostYarpTranslator.Translate([MakeHostWithHealth(FullActive, null, AvailableDestinationsPolicy.HealthyOrPanic)]);

        var health = clusters[0].HealthCheck!;
        Assert.Equal("HealthyOrPanic", health.AvailableDestinationsPolicy);
        Assert.Null(health.Passive);
        Assert.Equal(true, health.Active!.Enabled);
        Assert.Equal("ConsecutiveFailures", health.Active.Policy);
        Assert.Equal(TimeSpan.FromSeconds(20), health.Active.Interval);
        Assert.Equal(TimeSpan.FromSeconds(5), health.Active.Timeout);
        Assert.Equal("/health", health.Active.Path);
        Assert.Equal("?deep=1", health.Active.Query);
        Assert.Equal("3", clusters[0].Metadata![ConsecutiveFailuresHealthPolicyOptions.ThresholdMetadataName]);
        Assert.Equal("http://probe:9000/", clusters[0].Destinations!["primary"].Health);
    }

    [Fact]
    public void Translate_PassiveOnly_MapsRateLimitWithInvariantCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");
        try
        {
            var passive = new PassiveHealthCheck(PassiveHealthCheckPolicy.TransportFailureRate, TimeSpan.FromSeconds(60), 0.25);
            var (_, clusters) = ProxyHostYarpTranslator.Translate([MakeHostWithHealth(null, passive)]);

            var health = clusters[0].HealthCheck!;
            Assert.Null(health.Active);
            Assert.Equal(true, health.Passive!.Enabled);
            Assert.Equal("TransportFailureRate", health.Passive.Policy);
            Assert.Equal(TimeSpan.FromSeconds(60), health.Passive.ReactivationPeriod);
            Assert.Equal("0.25", clusters[0].Metadata![TransportFailureRateHealthPolicyOptions.FailureRateLimitMetadataName]);
            Assert.Null(clusters[0].Destinations!["primary"].Health);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public async Task Translate_FullyPopulatedHost_PassesYarpConfigValidation()
    {
        var passive = new PassiveHealthCheck(PassiveHealthCheckPolicy.TransportFailureRate, TimeSpan.FromSeconds(60), 0.25);
        var (_, clusters) = ProxyHostYarpTranslator.Translate([MakeHostWithHealth(FullActive, passive)]);

        // YARP's own validator rejects unknown policy names, so this guards the enum-name pass-through.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddReverseProxy();
        await using var provider = services.BuildServiceProvider();
        var validator = provider.GetRequiredService<IConfigValidator>();

        var errors = await validator.ValidateClusterAsync(clusters[0]);

        Assert.Empty(errors);
    }
}
