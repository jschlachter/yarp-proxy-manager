extern alias ProxyManagerApp;
using ProxyManagerApp::West94.AspNetCore.Authentication;
using System.Net;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace West94.ProxyManager.API.Tests.Unit.Authentication;

/// <summary>
/// A real <see cref="OidcTokenRefresher"/> wired to an in-memory token endpoint.
/// </summary>
internal static class TokenEndpointFakes
{
    public const string TokenEndpoint = "https://idp.example.com/token";

    public static OidcTokenRefresher CreateRefresher(FakeTokenEndpoint endpoint)
    {
        var services = new ServiceCollection();
        services.Configure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme, o =>
        {
            o.ClientId = "ypm";
            o.ClientSecret = "s3cret";
            o.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(
                new OpenIdConnectConfiguration { TokenEndpoint = TokenEndpoint });
        });
        var sp = services.BuildServiceProvider();

        return new OidcTokenRefresher(
            new FakeHttpClientFactory(endpoint),
            sp.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>(),
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<OidcTokenRefresher>.Instance);
    }

    public sealed class FakeTokenEndpoint(HttpStatusCode status, string body, TimeSpan delay = default) : HttpMessageHandler
    {
        private int callCount;

        public int CallCount => callCount;
        public string? LastRequestUri { get; private set; }
        public string? LastForm { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Interlocked.Increment(ref callCount);
            LastRequestUri = request.RequestUri!.ToString();
            LastForm = await request.Content!.ReadAsStringAsync(ct);
            await Task.Delay(delay, ct);
            return new HttpResponseMessage(status) { Content = new StringContent(body) };
        }
    }

    public sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}
