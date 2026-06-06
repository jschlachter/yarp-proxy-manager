namespace West94.ProxyManager.Core.AggregatesModel.UserAggregate;

/// <summary>Defines the permission level granted to an authorized Proxy Manager user.</summary>
public enum UserAccessLevel
{
    /// <summary>Full read/write access to proxy configuration and user management.</summary>
    Admin,

    /// <summary>Read-only access to proxy configuration; cannot modify users or routes.</summary>
    ReadOnly
}
