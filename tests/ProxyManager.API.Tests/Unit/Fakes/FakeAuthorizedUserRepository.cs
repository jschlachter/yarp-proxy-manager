using West94.ProxyManager.Core.AggregatesModel.UserAggregate;
using West94.ProxyManager.Core.DTOs;

namespace West94.ProxyManager.API.Tests.Unit.Fakes;

internal sealed class FakeAuthorizedUserRepository : IAuthorizedUserRepository
{
    private readonly List<AuthorizedUser> _users = [];

    public void Seed(params AuthorizedUser[] users) => _users.AddRange(users);

    public Task<AuthorizedUser?> GetBySubAsync(string sub, CancellationToken ct = default) =>
        Task.FromResult(_users.FirstOrDefault(u => u.Sub == sub));

    public Task<PagedResult<AuthorizedUser>> GetAllAsync(bool includeDeactivated, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _users.AsEnumerable();
        if (!includeDeactivated)
            query = query.Where(u => u.Status == UserStatus.Active);

        var ordered = query.OrderBy(u => u.DisplayName).ToList();
        var total = ordered.Count;
        var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult(new PagedResult<AuthorizedUser>(items, total, page, pageSize));
    }

    public Task AddAsync(AuthorizedUser user, CancellationToken ct = default)
    {
        _users.Add(user);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(AuthorizedUser user, CancellationToken ct = default) =>
        Task.CompletedTask;
}
