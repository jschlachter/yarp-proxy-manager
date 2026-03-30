namespace West94.ProxyManager.Core.Messages.Events;

/// <summary>Published to RabbitMQ when a proxy host is successfully created.</summary>
public sealed record ProxyHostCreatedEvent(
    Guid Id,
    IReadOnlyList<string> DomainNames,
    string Destination,
    bool IsEnabled,
    DateTimeOffset OccurredAt);
