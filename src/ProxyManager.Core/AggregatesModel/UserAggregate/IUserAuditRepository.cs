using West94.ProxyManager.Core.DTOs;

namespace West94.ProxyManager.Core.AggregatesModel.UserAggregate;

/// <summary>Persistence contract for <see cref="UserAuditEntry"/> records.</summary>
public interface IUserAuditRepository
{
    /// <summary>Appends a new audit entry to the log.</summary>
    /// <param name="entry">The audit entry to persist.</param>
    /// <param name="ct">Cancellation token.</param>
    Task AppendAsync(UserAuditEntry entry, CancellationToken ct = default);

    /// <summary>Returns a paginated, optionally filtered view of the audit log.</summary>
    /// <param name="subFilter">When provided, only entries whose <c>SubjectSub</c> equals this value are returned.</param>
    /// <param name="from">When provided, only entries at or after this timestamp are returned.</param>
    /// <param name="to">When provided, only entries at or before this timestamp are returned.</param>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Maximum number of results per page.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<PagedResult<UserAuditEntry>> QueryAsync(
        string? subFilter,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int page,
        int pageSize,
        CancellationToken ct = default);
}
