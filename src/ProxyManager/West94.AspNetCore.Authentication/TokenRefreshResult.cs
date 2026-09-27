using System.Text.Json.Serialization;

namespace West94.AspNetCore.Authentication;

/// <summary>
/// The token endpoint's response to a <c>refresh_token</c> grant.
/// </summary>
/// <param name="AccessToken">The new access token.</param>
/// <param name="RefreshToken">The rotated refresh token, or <c>null</c> when the provider keeps the old one.</param>
/// <param name="IdToken">A new id token, if the provider returned one.</param>
/// <param name="ExpiresIn">Lifetime of <paramref name="AccessToken"/> in seconds.</param>
public sealed record TokenRefreshResult(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("refresh_token")] string? RefreshToken,
    [property: JsonPropertyName("id_token")] string? IdToken,
    [property: JsonPropertyName("expires_in")] int ExpiresIn);
