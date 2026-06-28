namespace West94.ProxyManager.API.Options;

public sealed record AuditOptions
{
    public const string Section = "Audit";
    public int RetentionDays { get; init; } = 90;
}
