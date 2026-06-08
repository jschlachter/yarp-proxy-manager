namespace West94.ProxyManager.Core.DTOs;

/// <summary>Read model returned by the user audit log endpoint.</summary>
public sealed record UserAuditEntryDto(
    /// <summary>Unique identifier for this audit entry.</summary>
    Guid Id,
    /// <summary>The subject identifier of the user affected by the operation.</summary>
    string SubjectSub,
    /// <summary>The type of operation that was performed.</summary>
    West94.ProxyManager.Core.AggregatesModel.UserAggregate.UserOperation Operation,
    /// <summary>The access level before the change, if applicable.</summary>
    West94.ProxyManager.Core.AggregatesModel.UserAggregate.UserAccessLevel? PreviousAccessLevel,
    /// <summary>The access level after the change, if applicable.</summary>
    West94.ProxyManager.Core.AggregatesModel.UserAggregate.UserAccessLevel? NewAccessLevel,
    /// <summary>The subject identifier of the user who performed the operation.</summary>
    string ActorSub,
    /// <summary>UTC timestamp when the operation occurred.</summary>
    DateTimeOffset OccurredAt);
