namespace West94.ProxyManager.Files.Options;

public sealed record DatabaseOptions
{
    public const string Section = "Database";
    public string ConnectionString { get; init; } = string.Empty;
}
