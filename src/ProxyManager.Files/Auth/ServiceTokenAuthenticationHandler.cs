using System.Security.Claims;
using System.Text.Encodings.Web;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

using West94.ProxyManager.Files.Options;

namespace West94.ProxyManager.Files.Auth;

/// <summary>
/// Validates the shared-secret header sent by the API's service-to-service client. A second
/// scheme alongside the browser-facing JWT bearer scheme — <see cref="AuthorizationPolicyBuilder"/>
/// in Program.cs accepts either. See <see cref="ServiceTokenOptions"/> for why this is temporary.
/// </summary>
public sealed class ServiceTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<ServiceTokenOptions> serviceTokenOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ServiceToken";
    private const string HeaderName = "X-Files-Service-Token";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var sharedSecret = serviceTokenOptions.Value.SharedSecret;
        if (string.IsNullOrEmpty(sharedSecret) ||
            !Request.Headers.TryGetValue(HeaderName, out var provided) ||
            provided != sharedSecret)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "files-service-client")], SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
