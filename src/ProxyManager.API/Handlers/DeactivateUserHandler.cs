using Serilog;

using West94.ProxyManager.Core.AggregatesModel.UserAggregate;
using West94.ProxyManager.Core.Exceptions;
using West94.ProxyManager.Core.Messages.Commands;

namespace West94.ProxyManager.API.Handlers;

public sealed class DeactivateUserHandler(
    IAuthorizedUserRepository userRepository,
    IUserAuditRepository auditRepository)
{
    private static readonly Serilog.ILogger Logger = Log.ForContext<DeactivateUserHandler>();

    public async Task Handle(DeactivateUserCommand command, CancellationToken ct)
    {
        var user = await userRepository.GetBySubAsync(command.Sub, ct);

        if (user is null || user.Status == UserStatus.Deactivated)
            throw new UserNotFoundException(command.Sub);

        var previousLevel = user.AccessLevel;
        user.Deactivate();
        await userRepository.UpdateAsync(user, ct);

        await auditRepository.AppendAsync(
            UserAuditEntry.Create(command.Sub, UserOperation.Deactivated, previousLevel, null, command.ActorSub), ct);

        Logger.Information("User {Sub} deactivated (previous AccessLevel: {PreviousLevel}) by {ActorSub}",
            command.Sub, previousLevel, command.ActorSub);
    }
}
