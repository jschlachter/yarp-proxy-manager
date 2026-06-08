using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using Microsoft.Extensions.DependencyInjection;

using West94.ProxyManager.API.Tests.Helpers;
using West94.ProxyManager.Core.AggregatesModel.UserAggregate;
using West94.ProxyManager.Core.DTOs;
using West94.ProxyManager.Endpoints;

namespace West94.ProxyManager.API.Tests.Integration;

[Collection("Integration")]
public sealed class UserEndpointsTests : IAsyncDisposable
{
    private readonly TestWebAppFactory _factory;
    private readonly HttpClient _client;

    public UserEndpointsTests()
    {
        _factory = new TestWebAppFactory();
        _client = _factory.CreateClient(new() { AllowAutoRedirect = false });
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    private async Task SeedUserAsync(string sub = "sub|seed", bool deactivated = false)
    {
        var repo = _factory.Services.GetRequiredService<IAuthorizedUserRepository>();
        var user = AuthorizedUser.Create(sub, "Seed User", "Seed", "seed@example.com", null, UserAccessLevel.ReadOnly, "system");
        if (deactivated) user.Deactivate();
        await repo.AddAsync(user);
    }

    // ── GET /v1/users ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetUsers_WithValidToken_Returns200WithPagedResult()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwtFactory.CreateToken());

        var response = await _client.GetAsync("/v1/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<AuthorizedUserDto>>();
        Assert.NotNull(result);
        Assert.NotNull(result.Items);
    }

    [Fact]
    public async Task GetUsers_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/v1/users");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── GET /v1/users/{sub} ────────────────────────────────────────────────

    [Fact]
    public async Task GetUserBySub_ExistingUser_Returns200()
    {
        await SeedUserAsync("sub|get-test");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwtFactory.CreateToken());

        var response = await _client.GetAsync("/v1/users/sub%7Cget-test");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<AuthorizedUserDto>();
        Assert.NotNull(dto);
        Assert.Equal("sub|get-test", dto.Sub);
    }

    [Fact]
    public async Task GetUserBySub_UnknownUser_Returns404()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwtFactory.CreateToken());

        var response = await _client.GetAsync("/v1/users/nonexistent");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetUserBySub_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/v1/users/sub%7C1");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── POST /v1/users ─────────────────────────────────────────────────────

    [Fact]
    public async Task PostUser_AdminToken_NewUser_Returns201WithLocation()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwtFactory.CreateToken("actor|1", "Admin"));

        var body = new CreateUserRequest("sub|new-1", "New User", "New", "new@example.com", null, UserAccessLevel.ReadOnly);
        var response = await _client.PostAsJsonAsync("/v1/users", body);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Contains("new-1", response.Headers.Location.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PostUser_AdminToken_DeactivatedUser_Returns200WithReactivationHeader()
    {
        await SeedUserAsync("sub|react", deactivated: true);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwtFactory.CreateToken("actor|2", "Admin"));

        var body = new CreateUserRequest("sub|react", "Reactivated User", "React", "react@example.com", null, UserAccessLevel.Admin);
        var response = await _client.PostAsJsonAsync("/v1/users", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("X-User-Reactivated"));
        Assert.Equal("true", response.Headers.GetValues("X-User-Reactivated").First());
    }

    [Fact]
    public async Task PostUser_AdminToken_ActiveConflict_Returns409()
    {
        await SeedUserAsync("sub|conflict");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwtFactory.CreateToken("actor|3", "Admin"));

        var body = new CreateUserRequest("sub|conflict", "Conflict User", "Con", "conflict@example.com", null, UserAccessLevel.ReadOnly);
        var response = await _client.PostAsJsonAsync("/v1/users", body);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task PostUser_ReadOnlyToken_Returns403()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwtFactory.CreateToken("actor|4", "ReadOnly"));

        var body = new CreateUserRequest("sub|ro", "RO User", "RO", "ro@example.com", null, UserAccessLevel.ReadOnly);
        var response = await _client.PostAsJsonAsync("/v1/users", body);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PostUser_WithoutToken_Returns401()
    {
        var body = new CreateUserRequest("sub|anon", "Anon", "Anon", "anon@example.com", null, UserAccessLevel.ReadOnly);
        var response = await _client.PostAsJsonAsync("/v1/users", body);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── PATCH /v1/users/{sub} ──────────────────────────────────────────────

    [Fact]
    public async Task PatchUser_AdminToken_KnownUser_Returns200()
    {
        await SeedUserAsync("sub|patch-1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwtFactory.CreateToken("actor|5", "Admin"));

        var body = new UpdateUserAccessLevelRequest(UserAccessLevel.Admin);
        var response = await _client.PatchAsJsonAsync("/v1/users/sub%7Cpatch-1", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<AuthorizedUserDto>();
        Assert.NotNull(dto);
        Assert.Equal(UserAccessLevel.Admin, dto.AccessLevel);
    }

    [Fact]
    public async Task PatchUser_AdminToken_UnknownUser_Returns404()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwtFactory.CreateToken("actor|6", "Admin"));

        var body = new UpdateUserAccessLevelRequest(UserAccessLevel.Admin);
        var response = await _client.PatchAsJsonAsync("/v1/users/nonexistent", body);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PatchUser_ReadOnlyToken_Returns403()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwtFactory.CreateToken("actor|7", "ReadOnly"));

        var body = new UpdateUserAccessLevelRequest(UserAccessLevel.Admin);
        var response = await _client.PatchAsJsonAsync("/v1/users/some-sub", body);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PatchUser_WithoutToken_Returns401()
    {
        var body = new UpdateUserAccessLevelRequest(UserAccessLevel.Admin);
        var response = await _client.PatchAsJsonAsync("/v1/users/some-sub", body);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── DELETE /v1/users/{sub} ─────────────────────────────────────────────

    [Fact]
    public async Task DeleteUser_AdminToken_ActiveUser_Returns204()
    {
        await SeedUserAsync("sub|del-1");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwtFactory.CreateToken("actor|8", "Admin"));

        var response = await _client.DeleteAsync("/v1/users/sub%7Cdel-1");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_AdminToken_AlreadyDeactivated_Returns404()
    {
        await SeedUserAsync("sub|del-2", deactivated: true);
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwtFactory.CreateToken("actor|9", "Admin"));

        var response = await _client.DeleteAsync("/v1/users/sub%7Cdel-2");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_AdminToken_UnknownUser_Returns404()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwtFactory.CreateToken("actor|10", "Admin"));

        var response = await _client.DeleteAsync("/v1/users/nonexistent");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_ReadOnlyToken_Returns403()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwtFactory.CreateToken("actor|11", "ReadOnly"));

        var response = await _client.DeleteAsync("/v1/users/some-sub");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_WithoutToken_Returns401()
    {
        var response = await _client.DeleteAsync("/v1/users/some-sub");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_ThenGetWithIncludeDeactivated_UserAppearsAsDeactivated()
    {
        await SeedUserAsync("sub|del-verify");
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwtFactory.CreateToken("actor|12", "Admin"));

        await _client.DeleteAsync("/v1/users/sub%7Cdel-verify");

        var response = await _client.GetAsync("/v1/users?includeDeactivated=true");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<AuthorizedUserDto>>();
        Assert.NotNull(result);
        var deactivated = result.Items.FirstOrDefault(u => u.Sub == "sub|del-verify");
        Assert.NotNull(deactivated);
        Assert.Equal(UserStatus.Deactivated, deactivated.Status);
    }

    // ── GET /v1/users/audit ────────────────────────────────────────────────

    [Fact]
    public async Task GetAuditLog_WithValidToken_Returns200()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwtFactory.CreateToken());

        var response = await _client.GetAsync("/v1/users/audit");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<UserAuditEntryDto>>();
        Assert.NotNull(result);
    }

    [Fact]
    public async Task GetAuditLog_ReadOnlyToken_Returns200()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestJwtFactory.CreateToken("actor|ro", "ReadOnly"));

        var response = await _client.GetAsync("/v1/users/audit");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAuditLog_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/v1/users/audit");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
