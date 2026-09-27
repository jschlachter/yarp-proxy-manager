using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace West94.AspNetCore.Authentication;

/// <summary>
/// Exchanges a refresh token for new tokens at the OIDC provider's token endpoint.
/// </summary>
/// <remarks>
/// Registered as a singleton. With refresh-token rotation a refresh token is single-use, so parallel
/// requests carrying the same expired cookie must share one token-endpoint call: the in-flight call
/// is cached for <see cref="DedupeWindow"/> and every caller with that refresh token awaits it.
/// The cache is per process; see ADR 0001 for the multi-instance caveat.
/// </remarks>
public sealed class OidcTokenRefresher(
    IHttpClientFactory httpClientFactory,
    IOptionsMonitor<OpenIdConnectOptions> oidcOptions,
    IMemoryCache cache,
    ILogger<OidcTokenRefresher> logger)
{
    /// <summary>
    /// Name of the <see cref="HttpClient"/> used to call the token endpoint.
    /// </summary>
    public const string HttpClientName = nameof(OidcTokenRefresher);

    // Long enough to cover requests that raced with the refresh and still carry the old cookie.
    private static readonly TimeSpan DedupeWindow = TimeSpan.FromSeconds(30);

    private readonly Lock cacheLock = new();

    /// <summary>
    /// Refreshes the tokens for <paramref name="refreshToken"/>, sharing the result with any concurrent
    /// or recent caller that presents the same refresh token.
    /// </summary>
    /// <returns>The new tokens, or <c>null</c> if the provider rejected the refresh.</returns>
    public async Task<TokenRefreshResult?> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        // Key on a hash so the raw refresh token isn't held in the cache.
        var key = "oidc-refresh:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));

        Lazy<Task<TokenRefreshResult?>> refresh;
        // GetOrCreate isn't atomic: without the lock, racing callers each create (and send) their own refresh.
        lock (cacheLock)
        {
            refresh = cache.GetOrCreate(key, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = DedupeWindow;
                // The shared call must not be cancelled by whichever request happened to start it.
                return new Lazy<Task<TokenRefreshResult?>>(() => SendRefreshAsync(refreshToken, CancellationToken.None));
            })!;
        }

        try
        {
            var result = await refresh.Value.WaitAsync(ct);
            if (result is null)
            {
                // Don't pin a failure for the whole window; a later request may retry.
                cache.Remove(key);
            }

            return result;
        }
        catch (Exception) when (refresh.Value.IsFaulted)
        {
            cache.Remove(key);
            throw;
        }
    }

    private async Task<TokenRefreshResult?> SendRefreshAsync(string refreshToken, CancellationToken ct)
    {
        var options = oidcOptions.Get(OpenIdConnectDefaults.AuthenticationScheme);
        // The OIDC handler's configuration manager caches the discovery document.
        var configuration = await options.ConfigurationManager!.GetConfigurationAsync(ct);

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = options.ClientId ?? "",
            ["client_secret"] = options.ClientSecret ?? "",
        });

        var client = httpClientFactory.CreateClient(HttpClientName);
        using var response = await client.PostAsync(configuration.TokenEndpoint, content, ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Token refresh failed with {StatusCode} ({Error})",
                (int)response.StatusCode, await ReadErrorCodeAsync(response, ct));
            return null;
        }

        logger.LogInformation("Refreshed OIDC access token");
        return await response.Content.ReadFromJsonAsync<TokenRefreshResult>(ct);
    }

    // Reads the OAuth "error" code from an error response. Only the code is logged, never the body,
    // so nothing token-like can end up in the logs.
    private static async Task<string?> ReadErrorCodeAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            return doc.RootElement.TryGetProperty("error", out var error) ? error.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
