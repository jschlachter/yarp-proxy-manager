namespace West94.ProxyManager.Core.AggregatesModel.AuditLogAggregate;

public enum AuditOperation { Created, Updated, Deleted }

public sealed record AuditLogEntry
{
    public Guid Id { get; init; }
    public string ActorId { get; init; } = string.Empty;
    public AuditOperation Operation { get; init; }
    public Guid ProxyHostId { get; init; }
    public string? PreviousState { get; init; }
    public string? NewState { get; init; }
    public DateTimeOffset OccurredAt { get; init; }

    private AuditLogEntry() { }

    /// <summary>Creates a new immutable audit log entry stamped with the current UTC time.</summary>
    public static AuditLogEntry Create(
        string actorId,
        AuditOperation operation,
        Guid proxyHostId,
        string? previousState,
        string? newState)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);

        return new AuditLogEntry
        {
            Id = Guid.NewGuid(),
            ActorId = actorId,
            Operation = operation,
            ProxyHostId = proxyHostId,
            PreviousState = previousState,
            NewState = newState,
            OccurredAt = DateTimeOffset.UtcNow
        };
    }
}
