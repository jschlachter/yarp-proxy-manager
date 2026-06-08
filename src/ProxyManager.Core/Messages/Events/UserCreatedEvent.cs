using West94.ProxyManager.Core.AggregatesModel.UserAggregate;

namespace West94.ProxyManager.Core.Messages.Events;

/// <summary>Published when a new authorized user is successfully created.</summary>
public sealed record UserCreatedEvent(
    string Sub,
    string DisplayName,
    string Email,
    UserAccessLevel AccessLevel,
    string ActorSub,
    DateTimeOffset OccurredAt);
