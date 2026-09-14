namespace West94.ProxyManager.Infrastructure.Options;

/// <summary>Configuration for a service-to-service HTTP client into ProxyManager.Files.</summary>
public sealed record FilesServiceOptions
{
    public const string Section = "FilesService";

    public string BaseUrl { get; init; } = "http://localhost:5080";

    /// <summary>
    /// Shared secret sent as the <c>X-Files-Service-Token</c> header. Deliberate temporary
    /// shortcut pending OAuth2 client-credentials against Authentik (see files-service-plan.md Deferred #3).
    /// </summary>
    public string ServiceToken { get; init; } = string.Empty;
}
