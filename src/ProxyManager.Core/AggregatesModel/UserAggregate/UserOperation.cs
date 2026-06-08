namespace West94.ProxyManager.Core.AggregatesModel.UserAggregate;

/// <summary>Identifies the type of mutation recorded in a user audit entry.</summary>
public enum UserOperation
{
    /// <summary>A new user was added to the authorized list.</summary>
    Created,

    /// <summary>An existing user's access level was changed.</summary>
    Updated,

    /// <summary>A user's access was revoked (soft-deleted).</summary>
    Deactivated,

    /// <summary>A previously deactivated user was restored to active status.</summary>
    Reactivated
}
