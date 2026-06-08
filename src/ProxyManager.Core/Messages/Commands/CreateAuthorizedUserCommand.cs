using West94.ProxyManager.Core.AggregatesModel.UserAggregate;

namespace West94.ProxyManager.Core.Messages.Commands;

/// <summary>Adds a new user to the authorized list or reactivates a deactivated one.</summary>
public sealed record CreateAuthorizedUserCommand(
    string Sub,
    string DisplayName,
    string Nickname,
    string Email,
    string? ProfileImageUrl,
    UserAccessLevel AccessLevel,
    string ActorSub);
