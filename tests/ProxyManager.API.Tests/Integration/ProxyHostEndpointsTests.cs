using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using Microsoft.Extensions.DependencyInjection;

using West94.ProxyManager.API.Tests.Helpers;
using West94.ProxyManager.Core.AggregatesModel.ProxyHostAggregate;
using West94.ProxyManager.Core.DTOs;

namespace West94.ProxyManager.API.Tests.Integration;

public sealed class ProxyHostEndpointsTests : IAsyncDisposable
{
    private readonly TestWebAppFactory _factory;
    private readonly HttpClient _client;

    public ProxyHostEndpointsTests()
    {
        _factory = new TestWebAppFactory();
        _client = _factory.CreateClient(new() { AllowAutoRedirect = false });
    }

    private async Task<Guid> SeedHostAsync(string domain = "integration-test.example.com")
    {
        var repo = _factory.Services.GetRequiredService<IProxyHostRepository>();
        var host = ProxyHost.Create([domain], DestinationUri.Parse("http://backend:8080"));
        await repo.AddAsync(host);
        return host.Id;
    }

    [Fact]
    public async Task GetProxyHosts_WithValidToken_Returns200WithPagedResult()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwtFactory.CreateToken());

        var response = await _client.GetAsync("/proxyhosts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<ProxyHostDto>>();
        Assert.NotNull(result);
        Assert.NotNull(result.Items);
    }

    [Fact]
    public async Task GetProxyHosts_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/proxyhosts");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetProxyHostById_WithValidTokenAndExistingId_Returns200()
    {
        var seededId = await SeedHostAsync("byid-test.example.com");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwtFactory.CreateToken());

        var response = await _client.GetAsync($"/proxyhosts/{seededId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ProxyHostDto>();
        Assert.NotNull(result);
        Assert.Equal(seededId, result.Id);
    }

    [Fact]
    public async Task GetProxyHostById_WithUnknownId_Returns404()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwtFactory.CreateToken());

        var response = await _client.GetAsync($"/proxyhosts/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }
}
