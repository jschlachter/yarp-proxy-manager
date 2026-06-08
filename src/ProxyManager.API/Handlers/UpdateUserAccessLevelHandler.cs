using Serilog;

using West94.ProxyManager.Core.AggregatesModel.UserAggregate;
using West94.ProxyManager.Core.DTOs;
using West94.ProxyManager.Core.Exceptions;
using West94.ProxyManager.Core.Messages.Commands;

namespace West94.ProxyManager.API.Handlers;

public sealed class UpdateUserAccessLevelHandler(
    IAuthorizedUserRepository userRepository,
    IUserAuditRepository auditRepository)
{
    private static readonly Serilog.ILogger Logger = Log.ForContext<UpdateUserAccessLevelHandler>();

    public async Task<AuthorizedUserDto> Handle(UpdateUserAccessLevelCommand command, CancellationToken ct)
    {
        var user = await userRepository.GetBySubAsync(command.Sub, ct);

        if (user is null || user.Status == UserStatus.Deactivated)
            throw new UserNotFoundException(command.Sub);

        var previousLevel = user.AccessLevel;
        user.UpdateAccessLevel(command.NewAccessLevel);
        await userRepository.UpdateAsync(user, ct);

        await auditRepository.AppendAsync(
            UserAuditEntry.Create(command.Sub, UserOperation.Updated, previousLevel, command.NewAccessLevel, command.ActorSub), ct);

        Logger.Information("User {Sub} access level updated from {PreviousLevel} to {NewLevel} by {ActorSub}",
            command.Sub, previousLevel, command.NewAccessLevel, command.ActorSub);

        return GetAuthorizedUsersHandler.MapToDto(user);
    }
}
