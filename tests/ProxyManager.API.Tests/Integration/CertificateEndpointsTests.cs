using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using Microsoft.Extensions.DependencyInjection;

using West94.ProxyManager.API.Tests.Helpers;
using West94.ProxyManager.Core.AggregatesModel.CertificateAggregate;
using West94.ProxyManager.Core.AggregatesModel.ProxyHostAggregate;
using West94.ProxyManager.Core.DTOs;

namespace West94.ProxyManager.API.Tests.Integration;

[Trait("Category", "Integration")]
public sealed class CertificateEndpointsTests : IAsyncDisposable
{
    private readonly TestWebAppFactory _factory;
    private readonly HttpClient _client;

    public CertificateEndpointsTests()
    {
        _factory = new TestWebAppFactory();
        _client = _factory.CreateClient(new() { AllowAutoRedirect = false });
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwtFactory.CreateToken());
    }

    private async Task<Guid> SeedCertAsync(string name = "integration-cert")
    {
        var repo = _factory.Services.GetRequiredService<ICertificateRepository>();
        var cert = Certificate.Create(name, CertificateFormat.Pem, $"/certs/{name}.pem");
        await repo.AddAsync(cert);
        return cert.Id;
    }

    private async Task<Guid> SeedHostAsync(string domain = "cert-integration.example.com")
    {
        var repo = _factory.Services.GetRequiredService<IProxyHostRepository>();
        var host = ProxyHost.Create([domain], DestinationUri.Parse("http://backend:8080"));
        await repo.AddAsync(host);
        return host.Id;
    }

    // --- GET /certificates ---

    [Fact]
    public async Task GetCertificates_Returns200WithPagedResult()
    {
        var response = await _client.GetAsync("/certificates");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<CertificateDto>>();
        Assert.NotNull(result);
        Assert.NotNull(result.Items);
    }

    [Fact]
    public async Task GetCertificates_WithoutToken_Returns401()
    {
        using var anonClient = _factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await anonClient.GetAsync("/certificates");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // --- GET /certificates/{id} ---

    [Fact]
    public async Task GetCertificateById_ExistingId_Returns200WithDto()
    {
        var id = await SeedCertAsync("byid-cert");

        var response = await _client.GetAsync($"/certificates/{id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<CertificateDto>();
        Assert.NotNull(dto);
        Assert.Equal(id, dto.Id);
        Assert.Equal("byid-cert", dto.Name);
    }

    [Fact]
    public async Task GetCertificateById_UnknownId_Returns404()
    {
        var response = await _client.GetAsync($"/certificates/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- POST /certificates ---

    [Fact]
    public async Task CreateCertificate_ValidPemBody_Returns201WithLocationHeader()
    {
        var body = new
        {
            name = "new-pem-cert",
            format = "Pem",
            certificatePath = "/certs/new.pem",
            keyFilePath = "/certs/new.key"
        };

        var response = await _client.PostAsJsonAsync("/certificates", body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var dto = await response.Content.ReadFromJsonAsync<CertificateDto>();
        Assert.NotNull(dto);
        Assert.NotEqual(Guid.Empty, dto.Id);
        Assert.Equal("new-pem-cert", dto.Name);
        Assert.Equal("Pem", dto.Format);
        Assert.StartsWith("/certificates/", response.Headers.Location.ToString());
    }

    [Fact]
    public async Task CreateCertificate_ValidPfxBody_Returns201()
    {
        var body = new
        {
            name = "new-pfx-cert",
            format = "Pfx",
            certificatePath = "/certs/new.pfx",
            passPhrase = "secret"
        };

        var response = await _client.PostAsJsonAsync("/certificates", body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreateCertificate_PfxWithKeyFilePath_Returns400()
    {
        var body = new
        {
            name = "bad-pfx",
            format = "Pfx",
            certificatePath = "/certs/bad.pfx",
            keyFilePath = "/certs/bad.key"
        };

        var response = await _client.PostAsJsonAsync("/certificates", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateCertificate_MissingCertificatePath_Returns400()
    {
        var body = new { name = "no-path", format = "Pem" };

        var response = await _client.PostAsJsonAsync("/certificates", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateCertificate_InvalidFormat_Returns400()
    {
        var body = new { name = "bad-format", format = "DER", certificatePath = "/certs/cert.der" };

        var response = await _client.PostAsJsonAsync("/certificates", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateCertificate_WithoutToken_Returns401()
    {
        using var anonClient = _factory.CreateClient(new() { AllowAutoRedirect = false });
        var body = new { name = "noauth", format = "Pem", certificatePath = "/certs/cert.pem" };
        var response = await anonClient.PostAsJsonAsync("/certificates", body);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // --- PUT /certificates/{id} ---

    [Fact]
    public async Task UpdateCertificate_ValidRename_Returns200WithUpdatedName()
    {
        var id = await SeedCertAsync("rename-me");
        var body = new { name = "renamed" };

        var response = await _client.PutAsJsonAsync($"/certificates/{id}", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<CertificateDto>();
        Assert.NotNull(dto);
        Assert.Equal("renamed", dto.Name);
    }

    [Fact]
    public async Task UpdateCertificate_UnknownId_Returns404()
    {
        var response = await _client.PutAsJsonAsync($"/certificates/{Guid.NewGuid()}", new { name = "x" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- DELETE /certificates/{id} ---

    [Fact]
    public async Task DeleteCertificate_ExistingId_Returns204()
    {
        var id = await SeedCertAsync("delete-me");

        var response = await _client.DeleteAsync($"/certificates/{id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteCertificate_SecondDelete_Returns404()
    {
        var id = await SeedCertAsync("delete-twice");

        await _client.DeleteAsync($"/certificates/{id}");
        var response = await _client.DeleteAsync($"/certificates/{id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteCertificate_WithoutToken_Returns401()
    {
        using var anonClient = _factory.CreateClient(new() { AllowAutoRedirect = false });
        var response = await anonClient.DeleteAsync($"/certificates/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // --- PUT /proxyhosts/{id}/certificate ---

    [Fact]
    public async Task AssignCertificate_ValidCertId_Returns200WithUpdatedHost()
    {
        var hostId = await SeedHostAsync("assign-host.example.com");
        var certId = await SeedCertAsync("assign-cert");
        var body = new { certificateId = certId };

        var response = await _client.PutAsJsonAsync($"/proxyhosts/{hostId}/certificate", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<ProxyHostDto>();
        Assert.NotNull(dto);
        Assert.Equal(certId, dto.CertificateId);
    }

    [Fact]
    public async Task AssignCertificate_NullCertId_UnassignsCertificate()
    {
        var certId = await SeedCertAsync("to-unassign");
        var repo = _factory.Services.GetRequiredService<IProxyHostRepository>();
        var host = ProxyHost.Create(["unassign.example.com"], DestinationUri.Parse("http://backend:8080"), certId);
        await repo.AddAsync(host);
        var body = new { certificateId = (Guid?)null };

        var response = await _client.PutAsJsonAsync($"/proxyhosts/{host.Id}/certificate", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<ProxyHostDto>();
        Assert.NotNull(dto);
        Assert.Null(dto.CertificateId);
    }

    [Fact]
    public async Task AssignCertificate_UnknownHostId_Returns404()
    {
        var body = new { certificateId = (Guid?)null };
        var response = await _client.PutAsJsonAsync($"/proxyhosts/{Guid.NewGuid()}/certificate", body);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AssignCertificate_UnknownCertId_Returns400()
    {
        var hostId = await SeedHostAsync("badcert.example.com");
        var body = new { certificateId = Guid.NewGuid() };

        var response = await _client.PutAsJsonAsync($"/proxyhosts/{hostId}/certificate", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }
}
