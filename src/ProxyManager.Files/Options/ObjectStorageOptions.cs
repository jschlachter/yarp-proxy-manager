namespace West94.ProxyManager.Files.Options;

public sealed record ObjectStorageOptions
{
    public const string Section = "ObjectStorage";

    public string ServiceUrl { get; init; } = string.Empty;
    public string Bucket { get; init; } = "proxymanager";
    public string Region { get; init; } = "us-east-1";
    public string AccessKey { get; init; } = string.Empty;
    public string SecretKey { get; init; } = string.Empty;
    public bool ForcePathStyle { get; init; } = true;
    public bool AutoCreateBucket { get; init; } = true;
    public TimeSpan PresignedUrlTtl { get; init; } = TimeSpan.FromMinutes(15);
}
