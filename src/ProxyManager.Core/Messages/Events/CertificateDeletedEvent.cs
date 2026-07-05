using West94.ProxyManager.Core.SeedWork;

namespace West94.ProxyManager.Core.Messages.Events;

/// <summary>Published to RabbitMQ when a certificate is deleted.</summary>
public sealed record CertificateDeletedEvent(Guid Id, DateTimeOffset OccurredAt) : IDomainEvent;
