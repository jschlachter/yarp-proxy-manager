namespace West94.ProxyManager.Files.Contracts;

/// <summary>
/// Duplicated from <c>West94.ProxyManager.Core.DTOs.PagedResult</c> — ten lines beats a cross-service
/// type dependency from the generic Files service into the cert domain's Core project.
/// </summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize);
