using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace West94.AspNetCore.Authentication;

/// <summary>
/// Registration for silent OIDC token refresh in the cookie session.
/// </summary>
public static class OidcTokenRefreshExtensions
{
    /// <summary>
    /// Refreshes the cookie session's access token through the OIDC scheme's token endpoint before it expires.
    /// Call after <c>AddCookie</c> so this handler replaces any <c>OnValidatePrincipal</c> set there.
    /// </summary>
    /// <example>
    /// <code>
    /// services.AddAuthentication(...)
    ///     .AddCookie(...)
    ///     .AddOpenIdConnect(...)
    ///     .AddOidcTokenRefresh(configuration);
    /// </code>
    /// </example>
    public static AuthenticationBuilder AddOidcTokenRefresh(this AuthenticationBuilder builder, IConfiguration configuration)
    {
        var services = builder.Services;

        services.AddMemoryCache();
        services.AddHttpClient(OidcTokenRefresher.HttpClientName);
        services.AddSingleton<OidcTokenRefresher>();
        services.AddSingleton<CookieOidcRefreshHandler>();
        services.Configure<OidcTokenRefreshOptions>(configuration.GetSection(OidcTokenRefreshOptions.Section));

        services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
            .Configure<CookieOidcRefreshHandler>((options, handler) =>
                options.Events.OnValidatePrincipal = handler.ValidateAsync);

        return builder;
    }

    /// <summary>
    /// True for requests that are API calls (<c>/api</c>, <c>/manage/api</c>) rather than page navigations.
    /// A <c>fetch</c> can't follow a redirect to the identity provider, so these get a 401 instead.
    /// </summary>
    public static bool IsApiRequest(PathString path) =>
        path.StartsWithSegments("/api") || path.StartsWithSegments("/manage/api");
}
