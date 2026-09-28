extern alias ProxyManagerApp;

using ProxyManagerApp::West94.ProxyManager.Endpoints;
using Yarp.ReverseProxy.Model;

namespace West94.ProxyManager.API.Tests.Unit.Yarp;

[Trait("Category", "Unit")]
public class HealthStateMapperTests
{
    [Theory]
    // Active only
    [InlineData(DestinationHealth.Healthy, null, DestinationHealth.Healthy)]
    [InlineData(DestinationHealth.Unhealthy, null, DestinationHealth.Unhealthy)]
    [InlineData(DestinationHealth.Unknown, null, DestinationHealth.Unknown)]
    // Passive only
    [InlineData(null, DestinationHealth.Healthy, DestinationHealth.Healthy)]
    [InlineData(null, DestinationHealth.Unhealthy, DestinationHealth.Unhealthy)]
    [InlineData(null, DestinationHealth.Unknown, DestinationHealth.Unknown)]
    // Both
    [InlineData(DestinationHealth.Healthy, DestinationHealth.Healthy, DestinationHealth.Healthy)]
    [InlineData(DestinationHealth.Healthy, DestinationHealth.Unknown, DestinationHealth.Unknown)]
    [InlineData(DestinationHealth.Unknown, DestinationHealth.Unknown, DestinationHealth.Unknown)]
    [InlineData(DestinationHealth.Unhealthy, DestinationHealth.Healthy, DestinationHealth.Unhealthy)]
    [InlineData(DestinationHealth.Healthy, DestinationHealth.Unhealthy, DestinationHealth.Unhealthy)]
    [InlineData(DestinationHealth.Unknown, DestinationHealth.Unhealthy, DestinationHealth.Unhealthy)]
    public void Combine_ReturnsOverallStatus(DestinationHealth? active, DestinationHealth? passive, DestinationHealth expected)
    {
        Assert.Equal(expected, HealthStateMapper.Combine(active, passive));
    }
}
