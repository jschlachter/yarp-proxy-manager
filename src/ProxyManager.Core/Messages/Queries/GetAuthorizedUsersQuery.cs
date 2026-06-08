namespace West94.ProxyManager.Core.Messages.Queries;

/// <summary>Returns a paginated list of authorized users, optionally including deactivated accounts.</summary>
public sealed record GetAuthorizedUsersQuery(
    bool IncludeDeactivated = false,
    int Page = 1,
    int PageSize = 20);
