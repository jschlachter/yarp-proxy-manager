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

    // --- US2: POST /proxyhosts ---

    [Fact]
    public async Task CreateProxyHost_WithValidBody_Returns201WithLocationHeader()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwtFactory.CreateToken());
        var body = new { domainNames = new[] { "create-test.example.com" }, destinationUri = "http://backend:8080" };

        var response = await _client.PostAsJsonAsync("/proxyhosts", body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var dto = await response.Content.ReadFromJsonAsync<ProxyHostDto>();
        Assert.NotNull(dto);
        Assert.NotEqual(Guid.Empty, dto.Id);
        Assert.Contains("create-test.example.com", dto.DomainNames);
        Assert.StartsWith("/proxyhosts/", response.Headers.Location.ToString());
    }

    [Fact]
    public async Task CreateProxyHost_DuplicateHostname_Returns409()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwtFactory.CreateToken());
        await SeedHostAsync("duplicate.example.com");
        var body = new { domainNames = new[] { "duplicate.example.com" }, destinationUri = "http://backend:8080" };

        var response = await _client.PostAsJsonAsync("/proxyhosts", body);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateProxyHost_MissingDestinationUri_Returns400()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwtFactory.CreateToken());
        var body = new { domainNames = new[] { "missing-dest.example.com" } };

        var response = await _client.PostAsJsonAsync("/proxyhosts", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateProxyHost_WithoutToken_Returns401()
    {
        var body = new { domainNames = new[] { "noauth.example.com" }, destinationUri = "http://backend:8080" };

        var response = await _client.PostAsJsonAsync("/proxyhosts", body);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }
}
