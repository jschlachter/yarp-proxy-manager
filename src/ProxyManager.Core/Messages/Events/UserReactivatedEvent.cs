using West94.ProxyManager.Core.AggregatesModel.UserAggregate;

namespace West94.ProxyManager.Core.Messages.Events;

/// <summary>Published when a previously deactivated user is restored to active status.</summary>
public sealed record UserReactivatedEvent(
    string Sub,
    UserAccessLevel NewAccessLevel,
    string ActorSub,
    DateTimeOffset OccurredAt);
