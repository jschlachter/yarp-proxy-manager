namespace West94.ProxyManager.Acme;

/// <summary>Bounded exponential backoff for transient ProxyManager.Files failures while loading certificate bytes.</summary>
public sealed class FilesRetryOptions
{
    public const string Section = "FilesService:Retry";

    public int MaxAttempts { get; set; } = 6;

    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMilliseconds(500);

    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(8);
}
