extern alias ProxyManagerApp;

using System.Net;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

using West94.ProxyManager.API.Tests.Helpers;
using West94.ProxyManager.Core.AggregatesModel.ProxyHostAggregate;

namespace West94.ProxyManager.API.Tests.Integration;

/// <summary>
/// System routes and the proxy's own endpoints answer only on <c>Management:Hosts</c> (localhost in
/// <see cref="TestProxyAppFactory"/>); on a user domain the same paths go to that host's route (ADR 0005).
/// The user host's destination is dead, so a request it forwards comes back 502.
/// </summary>
[Trait("Category", "Integration")]
[Collection(TestProxyAppFactory.Collection)]
public sealed class ManagementHostScopingTests : IAsyncDisposable
{
    private const string UserDomain = "app.example.com";

    private readonly TestProxyAppFactory _factory = new();
    private readonly HttpClient _client;

    public ManagementHostScopingTests()
    {
        _client = _factory.CreateClient(new() { AllowAutoRedirect = false });
        _factory.SeedAndReload(ProxyHost.Create([UserDomain], DestinationUri.Parse("http://127.0.0.1:9")));
    }

    private Task<HttpResponseMessage> SendAsync(string path, string? host = null, bool authenticated = false)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (host is not null)
            request.Headers.Host = host;
        if (authenticated)
            request.Headers.Add(TestProxyAppFactory.TestUserHeader, "user-1");

        return _client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ManageApiPath_OnUserDomain_GoesToUserRoute()
    {
        var response = await SendAsync("/manage/api/x", UserDomain);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task ManageApiPath_OnManagementHost_StillRequiresSignIn()
    {
        var response = await SendAsync("/manage/api/x");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task HealthStates_OnUserDomain_GoesToUserRoute()
    {
        var response = await SendAsync("/manage/api/health-states", UserDomain, authenticated: true);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task HealthStates_OnManagementHost_IsServedByProxy()
    {
        var response = await SendAsync("/manage/api/health-states", authenticated: true);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_OnUserDomain_GoesToUserRoute()
    {
        var response = await SendAsync("/login", UserDomain);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task Login_OnManagementHost_ChallengesWithOidc()
    {
        var response = await SendAsync("/login");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("https://idp.invalid/authorize", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Startup_WithoutManagementHost_Fails()
    {
        await using var factory = new TestProxyAppFactory()
            .WithWebHostBuilder(b => b.UseSetting("Management:Hosts:0", ""));

        Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }
}
