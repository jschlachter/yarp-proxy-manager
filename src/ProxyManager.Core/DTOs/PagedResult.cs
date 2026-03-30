namespace West94.ProxyManager.Core.DTOs;

/// <summary>Generic paginated result wrapper returned by list endpoints.</summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize);
