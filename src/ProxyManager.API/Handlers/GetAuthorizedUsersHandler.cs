using West94.ProxyManager.Core.AggregatesModel.UserAggregate;
using West94.ProxyManager.Core.DTOs;
using West94.ProxyManager.Core.Messages.Queries;

namespace West94.ProxyManager.API.Handlers;

public sealed class GetAuthorizedUsersHandler(IAuthorizedUserRepository repository)
{
    public async Task<PagedResult<AuthorizedUserDto>> Handle(GetAuthorizedUsersQuery query, CancellationToken ct)
    {
        var paged = await repository.GetAllAsync(query.IncludeDeactivated, query.Page, query.PageSize, ct);
        var dtos = paged.Items.Select(MapToDto).ToList();
        return new PagedResult<AuthorizedUserDto>(dtos, paged.TotalCount, paged.Page, paged.PageSize);
    }

    internal static AuthorizedUserDto MapToDto(AuthorizedUser user) => new(
        user.Sub,
        user.DisplayName,
        user.Nickname,
        user.Email,
        user.ProfileImageUrl,
        user.AccessLevel,
        user.Status,
        user.CreatedAt,
        user.LastModifiedAt,
        user.DeactivatedAt);
}
