using West94.ProxyManager.Core.AggregatesModel.UserAggregate;
using West94.ProxyManager.Core.DTOs;

namespace West94.ProxyManager.API.Tests.Unit.Fakes;

internal sealed class FakeUserAuditRepository : IUserAuditRepository
{
    public List<UserAuditEntry> Entries { get; } = [];

    public Task AppendAsync(UserAuditEntry entry, CancellationToken ct = default)
    {
        Entries.Add(entry);
        return Task.CompletedTask;
    }

    public Task<PagedResult<UserAuditEntry>> QueryAsync(
        string? subFilter,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var query = Entries.AsEnumerable();

        if (subFilter is not null)
            query = query.Where(e => e.SubjectSub == subFilter);

        if (from.HasValue)
            query = query.Where(e => e.OccurredAt >= from.Value);

        if (to.HasValue)
            query = query.Where(e => e.OccurredAt <= to.Value);

        var ordered = query.OrderBy(e => e.OccurredAt).ToList();
        var total = ordered.Count;
        var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Task.FromResult(new PagedResult<UserAuditEntry>(items, total, page, pageSize));
    }
}
