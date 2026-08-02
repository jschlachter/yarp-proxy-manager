namespace West94.ProxyManager.Files.Options;

public sealed record UploadOptions
{
    public const string Section = "Upload";

    /// <summary>Hard ceiling enforced by the endpoint's own byte counter (413, not a connection reset).</summary>
    public long MaxUploadBytes { get; init; } = 10 * 1024 * 1024;

    /// <summary>How long an uncommitted (owner-less) staged asset survives before the sweeper deletes it.</summary>
    public TimeSpan StagingTtl { get; init; } = TimeSpan.FromMinutes(30);

    /// <summary>How often the sweeper checks for expired staged assets.</summary>
    public TimeSpan SweepInterval { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>Extension allowlist per asset type — never trust the client's content-type header or extension alone.</summary>
    public IReadOnlyDictionary<string, string[]> AllowedExtensions { get; init; } =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["certificate"] = [".pfx", ".p12", ".pem", ".crt", ".cer", ".key"],
        };
}
