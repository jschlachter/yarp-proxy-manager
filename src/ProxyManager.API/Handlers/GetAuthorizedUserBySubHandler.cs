using West94.ProxyManager.Core.AggregatesModel.UserAggregate;
using West94.ProxyManager.Core.DTOs;
using West94.ProxyManager.Core.Messages.Queries;

namespace West94.ProxyManager.API.Handlers;

public sealed class GetAuthorizedUserBySubHandler(IAuthorizedUserRepository repository)
{
    public async Task<AuthorizedUserDto?> Handle(GetAuthorizedUserBySubQuery query, CancellationToken ct)
    {
        var user = await repository.GetBySubAsync(query.Sub, ct);
        return user is null ? null : GetAuthorizedUsersHandler.MapToDto(user);
    }
}
