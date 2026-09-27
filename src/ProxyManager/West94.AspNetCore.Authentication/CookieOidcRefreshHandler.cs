using System.Globalization;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

namespace West94.AspNetCore.Authentication;

/// <summary>
/// Backs <see cref="CookieAuthenticationEvents.OnValidatePrincipal"/>: refreshes the access token stored
/// in the auth cookie before it expires.
/// </summary>
/// <remarks>
/// Refreshing here rather than in middleware updates the ticket that the rest of the request sees, so the
/// YARP <c>BearerToken</c> transform's <c>GetTokenAsync</c> forwards the new token on this same request.
/// </remarks>
public sealed class CookieOidcRefreshHandler(OidcTokenRefresher refresher, IOptions<OidcTokenRefreshOptions> options)
{
    /// <summary>
    /// Refreshes the tokens when the access token is expired or about to expire, and rejects the
    /// principal (signing out) when the session can't be refreshed.
    /// </summary>
    public async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        if (string.IsNullOrEmpty(context.Properties.GetTokenValue("access_token")))
        {
            await RejectAsync(context);
            return;
        }

        var now = DateTimeOffset.UtcNow;
        // A missing or unreadable expiry is treated as expired so we refresh rather than forward a dead token.
        var expiresAt = DateTimeOffset.TryParse(context.Properties.GetTokenValue("expires_at"),
            CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed) ? parsed : DateTimeOffset.MinValue;

        if (expiresAt > now + options.Value.RefreshLeeway)
        {
            return;
        }

        var refreshToken = context.Properties.GetTokenValue("refresh_token");
        var result = string.IsNullOrEmpty(refreshToken)
            ? null
            : await refresher.RefreshAsync(refreshToken, context.HttpContext.RequestAborted);

        if (result is null)
        {
            await RejectAsync(context);
            return;
        }

        context.Properties.UpdateTokenValue("access_token", result.AccessToken);
        context.Properties.UpdateTokenValue("expires_at",
            now.AddSeconds(result.ExpiresIn).ToString("o", CultureInfo.InvariantCulture));
        if (!string.IsNullOrEmpty(result.RefreshToken))
        {
            context.Properties.UpdateTokenValue("refresh_token", result.RefreshToken);
        }
        if (!string.IsNullOrEmpty(result.IdToken))
        {
            context.Properties.UpdateTokenValue("id_token", result.IdToken);
        }

        context.ShouldRenew = true;
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(context.Scheme.Name);
    }
}
