using West94.ProxyManager.Core.DTOs;

namespace West94.ProxyManager.Core.AggregatesModel.UserAggregate;

/// <summary>Persistence contract for the <see cref="AuthorizedUser"/> aggregate.</summary>
public interface IAuthorizedUserRepository
{
    /// <summary>Returns the user with the given subject identifier, or <c>null</c> if not found.</summary>
    /// <param name="sub">Authentik subject identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<AuthorizedUser?> GetBySubAsync(string sub, CancellationToken ct = default);

    /// <summary>Returns a paginated list of authorized users.</summary>
    /// <param name="includeDeactivated">When <c>true</c>, deactivated accounts are included in the result.</param>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Maximum number of results per page.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<PagedResult<AuthorizedUser>> GetAllAsync(bool includeDeactivated, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Persists a newly created user.</summary>
    /// <param name="user">The user to add.</param>
    /// <param name="ct">Cancellation token.</param>
    Task AddAsync(AuthorizedUser user, CancellationToken ct = default);

    /// <summary>Persists changes to an existing user record.</summary>
    /// <param name="user">The modified user.</param>
    /// <param name="ct">Cancellation token.</param>
    Task UpdateAsync(AuthorizedUser user, CancellationToken ct = default);
}
