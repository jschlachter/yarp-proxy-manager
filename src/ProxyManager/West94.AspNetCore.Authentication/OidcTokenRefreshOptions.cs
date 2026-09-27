namespace West94.AspNetCore.Authentication;

/// <summary>
/// Settings for silent access-token refresh, bound from <c>Authentication:TokenRefresh</c>.
/// </summary>
public sealed record OidcTokenRefreshOptions
{
    /// <summary>
    /// Configuration section the options are bound from.
    /// </summary>
    public const string Section = "Authentication:TokenRefresh";

    /// <summary>
    /// Refresh the access token when it expires within this window, so it doesn't expire
    /// while a proxied request is in flight. Defaults to 60 seconds.
    /// </summary>
    public TimeSpan RefreshLeeway { get; init; } = TimeSpan.FromSeconds(60);
}
