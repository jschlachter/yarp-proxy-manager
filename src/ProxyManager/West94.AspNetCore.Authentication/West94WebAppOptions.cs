namespace West94.AspNetCore.Authentication;

public sealed record West94WebAppOptions
{
    public string Authority { get; set;} = null!;
    public string ClientId { get; set; } = null!;
    public string ClientSecret { get; set; } = null!;

    /// <summary>
    /// The path to which the authentication provider will redirect after successful authentication.
    /// </summary>
    /// <remarks>
    /// If not specified, the default callback path will be used ("/signin-oidc").
    /// </remarks>
    public string? CallbackPath { get; init; }

    /// <summary>
    /// The response type to use when requesting tokens from the authentication provider.
    /// </summary>
    /// <remarks>
    /// Supports  `id_token`, `code`, or `code id_token`, defaults to `id_token` when ommitted. 
    public string? ResponseType { get; init; }

    /// <summary>
    /// Indicates whether to use refresh tokens to maintain the user's authentication session.
    /// </summary>
    /// <remarks>

    public bool UseRefreshTokens { get; init; } = true;
}