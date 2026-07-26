namespace West94.ProxyManager.Files.Options;

/// <summary>
/// Deliberate temporary shortcut for service-to-service auth (API → Files) pending OAuth2
/// client-credentials against Authentik (see files-service-plan.md Deferred #3).
/// </summary>
public sealed record ServiceTokenOptions
{
    public const string Section = "ServiceToken";

    public string SharedSecret { get; init; } = string.Empty;
}
