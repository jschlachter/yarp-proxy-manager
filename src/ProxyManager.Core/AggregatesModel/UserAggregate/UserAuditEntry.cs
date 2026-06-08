namespace West94.ProxyManager.Core.AggregatesModel.UserAggregate;

/// <summary>Immutable record of a single user management operation for audit purposes.</summary>
public sealed record UserAuditEntry
{
    /// <summary>Unique identifier for this audit entry.</summary>
    public Guid Id { get; init; }

    /// <summary>The subject identifier of the user who was affected.</summary>
    public string SubjectSub { get; init; } = string.Empty;

    /// <summary>The type of operation that was performed.</summary>
    public UserOperation Operation { get; init; }

    /// <summary>The access level before the change, populated for <see cref="UserOperation.Updated"/> and <see cref="UserOperation.Reactivated"/>.</summary>
    public UserAccessLevel? PreviousAccessLevel { get; init; }

    /// <summary>The access level after the change, populated for <see cref="UserOperation.Created"/>, <see cref="UserOperation.Updated"/>, and <see cref="UserOperation.Reactivated"/>.</summary>
    public UserAccessLevel? NewAccessLevel { get; init; }

    /// <summary>The subject identifier of the user who performed the operation.</summary>
    public string ActorSub { get; init; } = string.Empty;

    /// <summary>UTC timestamp when the operation occurred.</summary>
    public DateTimeOffset OccurredAt { get; init; }

    private UserAuditEntry() { }

    /// <summary>
    /// Creates a new immutable audit entry stamped with the current UTC time.
    /// </summary>
    /// <param name="subjectSub">Subject identifier of the affected user.</param>
    /// <param name="operation">The operation performed.</param>
    /// <param name="previousAccessLevel">Access level before the change (optional).</param>
    /// <param name="newAccessLevel">Access level after the change (optional).</param>
    /// <param name="actorSub">Subject identifier of the actor who performed the operation.</param>
    public static UserAuditEntry Create(
        string subjectSub,
        UserOperation operation,
        UserAccessLevel? previousAccessLevel,
        UserAccessLevel? newAccessLevel,
        string actorSub) =>
        new()
        {
            Id = Guid.NewGuid(),
            SubjectSub = subjectSub,
            Operation = operation,
            PreviousAccessLevel = previousAccessLevel,
            NewAccessLevel = newAccessLevel,
            ActorSub = actorSub,
            OccurredAt = DateTimeOffset.UtcNow
        };
}
