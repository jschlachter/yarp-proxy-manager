using Serilog;

using West94.ProxyManager.Core.AggregatesModel.UserAggregate;
using West94.ProxyManager.Core.Exceptions;
using West94.ProxyManager.Core.Messages.Commands;
using West94.ProxyManager.Endpoints;

namespace West94.ProxyManager.API.Handlers;

public sealed class CreateAuthorizedUserHandler(
    IAuthorizedUserRepository userRepository,
    IUserAuditRepository auditRepository)
{
    private static readonly Serilog.ILogger Logger = Log.ForContext<CreateAuthorizedUserHandler>();

    public async Task<CreateUserResult> Handle(CreateAuthorizedUserCommand command, CancellationToken ct)
    {
        var existing = await userRepository.GetBySubAsync(command.Sub, ct);

        if (existing is not null)
        {
            if (existing.Status == UserStatus.Active)
                throw new UserConflictException(command.Sub);

            existing.Reactivate(command.AccessLevel);
            await userRepository.UpdateAsync(existing, ct);

            await auditRepository.AppendAsync(
                UserAuditEntry.Create(command.Sub, UserOperation.Reactivated, null, command.AccessLevel, command.ActorSub), ct);

            Logger.Information("User {Sub} reactivated with AccessLevel {AccessLevel} by {ActorSub}",
                command.Sub, command.AccessLevel, command.ActorSub);

            return new CreateUserResult(GetAuthorizedUsersHandler.MapToDto(existing), Reactivated: true);
        }

        var user = AuthorizedUser.Create(
            command.Sub,
            command.DisplayName,
            command.Nickname,
            command.Email,
            command.ProfileImageUrl,
            command.AccessLevel,
            command.ActorSub);

        await userRepository.AddAsync(user, ct);

        await auditRepository.AppendAsync(
            UserAuditEntry.Create(command.Sub, UserOperation.Created, null, command.AccessLevel, command.ActorSub), ct);

        Logger.Information("User {Sub} created with AccessLevel {AccessLevel} by {ActorSub}",
            command.Sub, command.AccessLevel, command.ActorSub);

        return new CreateUserResult(GetAuthorizedUsersHandler.MapToDto(user), Reactivated: false);
    }
}
