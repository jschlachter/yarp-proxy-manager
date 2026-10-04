extern alias ProxyManagerApp;

using System.Net;
using System.Net.Http.Json;

using ProxyManagerApp::West94.ProxyManager.Endpoints;
using West94.ProxyManager.API.Tests.Helpers;
using West94.ProxyManager.Core.AggregatesModel.ProxyHostAggregate;

namespace West94.ProxyManager.API.Tests.Integration;

[Trait("Category", "Integration")]
[Collection(TestProxyAppFactory.Collection)]
public sealed class HealthStateEndpointsTests : IAsyncDisposable
{
    private readonly TestProxyAppFactory _factory = new();

    private HttpClient CreateClient() => _factory.CreateClient(new() { AllowAutoRedirect = false });

    [Fact]
    public async Task GetHealthStates_Authenticated_IsServedByProxyAndListsCheckedHosts()
    {
        var client = CreateClient();
        var withChecks = ProxyHost.Create(["checked.example.com"], DestinationUri.Parse("http://backend:8080"),
            healthCheck: new HealthCheckSettings(
                new ActiveHealthCheck(ActiveHealthCheckPolicy.ConsecutiveFailures, interval: TimeSpan.FromHours(1)),
                new PassiveHealthCheck(PassiveHealthCheckPolicy.TransportFailureRate),
                AvailableDestinationsPolicy.HealthyAndUnknown));
        var withoutChecks = ProxyHost.Create(["plain.example.com"], DestinationUri.Parse("http://backend:8080"));
        _factory.SeedAndReload(withChecks, withoutChecks);

        var request = new HttpRequestMessage(HttpMethod.Get, "/manage/api/health-states");
        request.Headers.Add(TestProxyAppFactory.TestUserHeader, "user-1");
        var response = await client.SendAsync(request);

        // A forward to ui-api-route would hit the dead ui-cluster destination and return 502.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        var states = await response.Content.ReadFromJsonAsync<HealthStateDto[]>();
        var state = Assert.Single(states!);
        Assert.Equal(withChecks.Id, state.ProxyHostId);
        Assert.Equal("Unknown", state.Status);
        Assert.Equal("Unknown", state.Active);
        Assert.Equal("Unknown", state.Passive);
    }

    [Fact]
    public async Task GetHealthStates_Unauthenticated_Returns401()
    {
        var response = await CreateClient().GetAsync("/manage/api/health-states");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OtherManageApiPaths_AreStillForwardedToUi()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/manage/api/routes");
        request.Headers.Add(TestProxyAppFactory.TestUserHeader, "user-1");

        var response = await CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    public ValueTask DisposeAsync() => _factory.DisposeAsync();
}
