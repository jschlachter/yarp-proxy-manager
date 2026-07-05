using West94.ProxyManager.Core.SeedWork;

namespace West94.ProxyManager.Core.Messages.Events;

/// <summary>Published to RabbitMQ when a certificate is updated.</summary>
public sealed record CertificateUpdatedEvent(
    Guid Id,
    string Name,
    DateTimeOffset OccurredAt) : IDomainEvent;
