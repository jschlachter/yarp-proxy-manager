namespace West94.ProxyManager.Core.DTOs;

/// <summary>Read model returned by user query endpoints.</summary>
public sealed record AuthorizedUserDto(
    /// <summary>Authentik subject identifier (opaque string).</summary>
    string Sub,
    /// <summary>User's full display name.</summary>
    string DisplayName,
    /// <summary>User's preferred short name for UI display.</summary>
    string Nickname,
    /// <summary>User's email address.</summary>
    string Email,
    /// <summary>Optional absolute URL of the user's profile image.</summary>
    string? ProfileImageUrl,
    /// <summary>The permission level granted to this user.</summary>
    West94.ProxyManager.Core.AggregatesModel.UserAggregate.UserAccessLevel AccessLevel,
    /// <summary>Current lifecycle status of the user account.</summary>
    West94.ProxyManager.Core.AggregatesModel.UserAggregate.UserStatus Status,
    /// <summary>UTC timestamp when the user was first authorized.</summary>
    DateTimeOffset CreatedAt,
    /// <summary>UTC timestamp of the last modification to the user record.</summary>
    DateTimeOffset LastModifiedAt,
    /// <summary>UTC timestamp when the user was deactivated, if applicable.</summary>
    DateTimeOffset? DeactivatedAt);
