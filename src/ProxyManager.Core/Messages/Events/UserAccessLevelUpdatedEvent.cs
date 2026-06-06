using West94.ProxyManager.Core.AggregatesModel.UserAggregate;

namespace West94.ProxyManager.Core.Messages.Events;

/// <summary>Published when an authorized user's access level is changed.</summary>
public sealed record UserAccessLevelUpdatedEvent(
    string Sub,
    UserAccessLevel PreviousAccessLevel,
    UserAccessLevel NewAccessLevel,
    string ActorSub,
    DateTimeOffset OccurredAt);
