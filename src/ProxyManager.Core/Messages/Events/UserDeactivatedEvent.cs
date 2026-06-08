namespace West94.ProxyManager.Core.Messages.Events;

/// <summary>Published when an authorized user is soft-deleted.</summary>
public sealed record UserDeactivatedEvent(
    string Sub,
    string ActorSub,
    DateTimeOffset OccurredAt);
