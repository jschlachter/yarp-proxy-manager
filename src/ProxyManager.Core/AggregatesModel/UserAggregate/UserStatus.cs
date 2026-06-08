namespace West94.ProxyManager.Core.AggregatesModel.UserAggregate;

/// <summary>Represents the lifecycle state of an authorized user account.</summary>
public enum UserStatus
{
    /// <summary>The user account is active and can authenticate.</summary>
    Active,

    /// <summary>The user account has been soft-deleted; access is revoked but the record is retained.</summary>
    Deactivated
}
