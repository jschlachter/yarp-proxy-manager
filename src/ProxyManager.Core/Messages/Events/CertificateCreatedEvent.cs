using West94.ProxyManager.Core.SeedWork;

namespace West94.ProxyManager.Core.Messages.Events;

/// <summary>Published to RabbitMQ when a certificate is created.</summary>
public sealed record CertificateCreatedEvent(
    Guid Id,
    string Name,
    string Format,
    DateTimeOffset OccurredAt) : IDomainEvent;
