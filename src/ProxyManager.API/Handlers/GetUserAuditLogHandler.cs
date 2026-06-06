using West94.ProxyManager.Core.AggregatesModel.UserAggregate;
using West94.ProxyManager.Core.DTOs;
using West94.ProxyManager.Core.Messages.Queries;

namespace West94.ProxyManager.API.Handlers;

public sealed class GetUserAuditLogHandler(IUserAuditRepository auditRepository)
{
    public async Task<PagedResult<UserAuditEntryDto>> Handle(GetUserAuditLogQuery query, CancellationToken ct)
    {
        var paged = await auditRepository.QueryAsync(
            query.SubFilter, query.From, query.To, query.Page, query.PageSize, ct);

        var dtos = paged.Items.Select(MapToDto).ToList();
        return new PagedResult<UserAuditEntryDto>(dtos, paged.TotalCount, paged.Page, paged.PageSize);
    }

    internal static UserAuditEntryDto MapToDto(UserAuditEntry entry) => new(
        entry.Id,
        entry.SubjectSub,
        entry.Operation,
        entry.PreviousAccessLevel,
        entry.NewAccessLevel,
        entry.ActorSub,
        entry.OccurredAt);
}
