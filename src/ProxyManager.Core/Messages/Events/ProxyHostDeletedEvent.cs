namespace West94.ProxyManager.Core.Messages.Events;

/// <summary>Published to RabbitMQ when a proxy host is permanently deleted.</summary>
public sealed record ProxyHostDeletedEvent(
    Guid Id,
    IReadOnlyList<string> DomainNames,
    DateTimeOffset OccurredAt);
