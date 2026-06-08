using System.Collections.Concurrent;
using West94.ProxyManager.Core.AggregatesModel.UserAggregate;
using West94.ProxyManager.Core.DTOs;

namespace West94.ProxyManager.Infrastructure.Repositories;

/// <summary>Thread-safe in-memory implementation of <see cref="IAuthorizedUserRepository"/>.</summary>
public sealed class InMemoryAuthorizedUserRepository : IAuthorizedUserRepository
{
    private readonly ConcurrentDictionary<string, AuthorizedUser> _store = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public Task<AuthorizedUser?> GetBySubAsync(string sub, CancellationToken ct = default)
    {
        _store.TryGetValue(sub, out var user);
        return Task.FromResult(user);
    }

    /// <inheritdoc/>
    public Task<PagedResult<AuthorizedUser>> GetAllAsync(bool includeDeactivated, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _store.Values.AsEnumerable();

        if (!includeDeactivated)
            query = query.Where(u => u.Status == UserStatus.Active);

        var ordered = query.OrderBy(u => u.DisplayName).ToList();
        var total = ordered.Count;
        var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Task.FromResult(new PagedResult<AuthorizedUser>(items, total, page, pageSize));
    }

    /// <inheritdoc/>
    public Task AddAsync(AuthorizedUser user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (!_store.TryAdd(user.Sub, user))
            throw new InvalidOperationException($"A user with sub '{user.Sub}' already exists in the store.");

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task UpdateAsync(AuthorizedUser user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (!_store.ContainsKey(user.Sub))
            throw new InvalidOperationException($"User with sub '{user.Sub}' was not found in the store.");

        _store[user.Sub] = user;
        return Task.CompletedTask;
    }
}
