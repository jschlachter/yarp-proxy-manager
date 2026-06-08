namespace West94.ProxyManager.Core.Messages.Queries;

/// <summary>Returns a paginated, optionally filtered view of the user audit log.</summary>
public sealed record GetUserAuditLogQuery(
    string? SubFilter = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Page = 1,
    int PageSize = 20);
