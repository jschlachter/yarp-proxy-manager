using West94.ProxyManager.Core.AggregatesModel.UserAggregate;

namespace West94.ProxyManager.Core.Messages.Commands;

/// <summary>Changes the access level of an existing active user.</summary>
public sealed record UpdateUserAccessLevelCommand(
    string Sub,
    UserAccessLevel NewAccessLevel,
    string ActorSub);
