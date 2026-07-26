namespace West94.ProxyManager.API.Infrastructure.Files;

/// <summary>
/// Minimal client-side shape of the Files service's <c>FileAssetDto</c> — only the fields this
/// API needs (status for reconciliation, file name for denormalization). Duplicated rather than
/// shared, the same trade-off Files itself made for <c>PagedResult</c>: ten lines beats a
/// cross-service type dependency.
/// </summary>
public sealed record FileAssetSummary(Guid Id, string FileName, string Status);
