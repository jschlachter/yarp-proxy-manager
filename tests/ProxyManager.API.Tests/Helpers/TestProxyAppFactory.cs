extern alias ProxyManagerApp;

using System.Security.Claims;
using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

using ProxyManagerApp::West94.ProxyManager.Yarp;
using West94.ProxyManager.API.Tests.Unit.Fakes;
using West94.ProxyManager.Core.AggregatesModel.ProxyHostAggregate;

namespace West94.ProxyManager.API.Tests.Helpers;

/// <summary>
/// Test host for the proxy app (ProxyManager). Uses an in-memory proxy host repository, disables
/// RabbitMQ, keeps OIDC offline with a static configuration, and authenticates any request that
/// carries <see cref="TestUserHeader"/>. Adds a <c>ui-api-route</c> matching the production system
/// route so tests can assert which endpoint answers a <c>/manage/api</c> path.
/// </summary>
internal sealed class TestProxyAppFactory : WebApplicationFactory<ProxyManagerApp::Program>
{
    public const string TestUserHeader = "X-Test-User";

    /// <summary>
    /// xunit collection for test classes that start this host. Program.cs replaces and freezes the static
    /// Serilog bootstrap logger, so two proxy hosts starting in parallel fail with "logger is already frozen".
    /// </summary>
    public const string Collection = "ProxyApp";

    public FakeProxyHostRepository ProxyHosts { get; } = new();

    /// <summary>Seeds hosts and reloads YARP's database-backed config, as a change event would.</summary>
    public void SeedAndReload(params ProxyHost[] hosts)
    {
        ProxyHosts.Seed(hosts);
        Services.GetRequiredService<IProxyConfigReloader>().Reload();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // UseSetting rather than ConfigureAppConfiguration: Program.cs reads RabbitMQ:Enabled (and the
        // YARP/auth sections) while building, before late-added configuration sources apply.
        var settings = new Dictionary<string, string>
        {
            ["RabbitMQ:Enabled"] = "false",
            ["Database:ConnectionString"] = "Host=unused",
            ["FilesService:BaseUrl"] = "http://files.invalid",
            ["Authentication:Authority"] = "https://idp.invalid",
            ["Authentication:ClientId"] = "test-client",
            // TestServer sends Host: localhost, so the system routes and endpoints answer on it (ADR 0005).
            ["Management:Hosts:0"] = "localhost",
            ["ReverseProxy:Routes:ui-api-route:ClusterId"] = "ui-cluster",
            ["ReverseProxy:Routes:ui-api-route:AuthorizationPolicy"] = "AuthenticatedUsersOnly",
            ["ReverseProxy:Routes:ui-api-route:Order"] = "1",
            ["ReverseProxy:Routes:ui-api-route:Match:Path"] = "/manage/api/{**catch-all}",
            // Nothing listens here: a forwarded request would come back 502, not the endpoint's JSON.
            ["ReverseProxy:Clusters:ui-cluster:Destinations:primary:Address"] = "http://127.0.0.1:9"
        };
        foreach (var (key, value) in settings)
            builder.UseSetting(key, value);

        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IProxyHostRepository>(ProxyHosts);

            services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, HeaderAuthHandler>(HeaderAuthHandler.SchemeName, null);
            services.PostConfigure<AuthenticationOptions>(o => o.DefaultAuthenticateScheme = HeaderAuthHandler.SchemeName);

            // Challenges still go through OIDC (and its 401-for-API-paths event) without fetching metadata.
            services.PostConfigure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, o =>
                o.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(
                    new OpenIdConnectConfiguration { AuthorizationEndpoint = "https://idp.invalid/authorize" }));
        });
    }

    private sealed class HeaderAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "TestHeader";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(TestUserHeader, out var user))
                return Task.FromResult(AuthenticateResult.NoResult());

            var identity = new ClaimsIdentity([new Claim("sub", user.ToString())], SchemeName);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
    }
}
