extern alias ProxyManagerApp;
using ProxyManagerApp::West94.AspNetCore.Authentication;
using System.Globalization;
using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace West94.ProxyManager.API.Tests.Unit.Authentication;

[Trait("Category", "Unit")]
public class CookieOidcRefreshHandlerTests
{
    private const string SuccessBody = """{"access_token":"new-access","expires_in":300}""";

    private static (CookieValidatePrincipalContext Context, FakeAuthenticationService Auth) CreateContext(
        string? expiresAt, string? refreshToken = "old-refresh")
    {
        var auth = new FakeAuthenticationService();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddSingleton<IAuthenticationService>(auth).BuildServiceProvider(),
        };

        var tokens = new List<AuthenticationToken>
        {
            new() { Name = "access_token", Value = "old-access" },
            new() { Name = "id_token", Value = "old-id" },
        };
        if (expiresAt is not null)
        {
            tokens.Add(new() { Name = "expires_at", Value = expiresAt });
        }
        if (refreshToken is not null)
        {
            tokens.Add(new() { Name = "refresh_token", Value = refreshToken });
        }
        var properties = new AuthenticationProperties();
        properties.StoreTokens(tokens);

        var scheme = new AuthenticationScheme(CookieAuthenticationDefaults.AuthenticationScheme, null, typeof(CookieAuthenticationHandler));
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity("test")), properties, scheme.Name);

        return (new CookieValidatePrincipalContext(httpContext, scheme, new CookieAuthenticationOptions(), ticket), auth);
    }

    private static CookieOidcRefreshHandler CreateHandler(TokenEndpointFakes.FakeTokenEndpoint endpoint) =>
        new(TokenEndpointFakes.CreateRefresher(endpoint), Microsoft.Extensions.Options.Options.Create(new OidcTokenRefreshOptions()));

    private static string ExpiresIn(TimeSpan span) =>
        DateTimeOffset.UtcNow.Add(span).ToString("o", CultureInfo.InvariantCulture);

    [Fact]
    public async Task ValidateAsync_TokenNotExpiring_DoesNotRefresh()
    {
        var endpoint = new TokenEndpointFakes.FakeTokenEndpoint(HttpStatusCode.OK, SuccessBody);
        var (context, auth) = CreateContext(ExpiresIn(TimeSpan.FromMinutes(10)));

        await CreateHandler(endpoint).ValidateAsync(context);

        Assert.Equal(0, endpoint.CallCount);
        Assert.NotNull(context.Principal);
        Assert.False(context.ShouldRenew);
        Assert.False(auth.SignedOut);
    }

    [Fact]
    public async Task ValidateAsync_TokenExpiring_UpdatesTokensKeepsRefreshTokenAndRenews()
    {
        var endpoint = new TokenEndpointFakes.FakeTokenEndpoint(HttpStatusCode.OK, SuccessBody);
        var (context, _) = CreateContext(ExpiresIn(TimeSpan.FromSeconds(30)));

        await CreateHandler(endpoint).ValidateAsync(context);

        Assert.Equal(1, endpoint.CallCount);
        Assert.Equal("new-access", context.Properties.GetTokenValue("access_token"));
        Assert.Equal("old-refresh", context.Properties.GetTokenValue("refresh_token"));
        var expiresAt = DateTimeOffset.Parse(context.Properties.GetTokenValue("expires_at")!, CultureInfo.InvariantCulture);
        Assert.InRange(expiresAt, DateTimeOffset.UtcNow.AddSeconds(290), DateTimeOffset.UtcNow.AddSeconds(301));
        Assert.True(context.ShouldRenew);
        Assert.NotNull(context.Principal);
    }

    [Fact]
    public async Task ValidateAsync_TokenExpiring_StoresRotatedRefreshAndIdToken()
    {
        var endpoint = new TokenEndpointFakes.FakeTokenEndpoint(HttpStatusCode.OK,
            """{"access_token":"new-access","refresh_token":"new-refresh","id_token":"new-id","expires_in":300}""");
        var (context, _) = CreateContext(ExpiresIn(TimeSpan.FromSeconds(-30)));

        await CreateHandler(endpoint).ValidateAsync(context);

        Assert.Equal("new-refresh", context.Properties.GetTokenValue("refresh_token"));
        Assert.Equal("new-id", context.Properties.GetTokenValue("id_token"));
    }

    [Fact]
    public async Task ValidateAsync_RefreshFails_RejectsAndSignsOutWithoutRenewing()
    {
        var endpoint = new TokenEndpointFakes.FakeTokenEndpoint(HttpStatusCode.BadRequest, """{"error":"invalid_grant"}""");
        var (context, auth) = CreateContext(ExpiresIn(TimeSpan.FromSeconds(-30)));

        await CreateHandler(endpoint).ValidateAsync(context);

        Assert.Null(context.Principal);
        Assert.False(context.ShouldRenew);
        Assert.True(auth.SignedOut);
    }

    [Fact]
    public async Task ValidateAsync_ExpiredWithoutRefreshToken_Rejects()
    {
        var endpoint = new TokenEndpointFakes.FakeTokenEndpoint(HttpStatusCode.OK, SuccessBody);
        var (context, auth) = CreateContext(ExpiresIn(TimeSpan.FromSeconds(-30)), refreshToken: null);

        await CreateHandler(endpoint).ValidateAsync(context);

        Assert.Equal(0, endpoint.CallCount);
        Assert.Null(context.Principal);
        Assert.True(auth.SignedOut);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-date")]
    public async Task ValidateAsync_MissingOrGarbageExpiry_TreatedAsExpired(string? expiresAt)
    {
        var endpoint = new TokenEndpointFakes.FakeTokenEndpoint(HttpStatusCode.OK, SuccessBody);
        var (context, _) = CreateContext(expiresAt);

        await CreateHandler(endpoint).ValidateAsync(context);

        Assert.Equal(1, endpoint.CallCount);
        Assert.Equal("new-access", context.Properties.GetTokenValue("access_token"));
    }

    [Theory]
    [InlineData("/manage/api/routes", true)]
    [InlineData("/api/proxyhosts", true)]
    [InlineData("/manage/routes", false)]
    [InlineData("/", false)]
    public void IsApiRequest_MatchesApiPathsOnly(string path, bool expected)
    {
        Assert.Equal(expected, OidcTokenRefreshExtensions.IsApiRequest(path));
    }

    private sealed class FakeAuthenticationService : IAuthenticationService
    {
        public bool SignedOut { get; private set; }

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            SignedOut = true;
            return Task.CompletedTask;
        }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) => throw new NotSupportedException();
        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => throw new NotSupportedException();
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => throw new NotSupportedException();
        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) => throw new NotSupportedException();
    }
}
