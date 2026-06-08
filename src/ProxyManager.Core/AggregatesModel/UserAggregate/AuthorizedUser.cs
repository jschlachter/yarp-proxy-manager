using West94.ProxyManager.Core.Exceptions;

namespace West94.ProxyManager.Core.AggregatesModel.UserAggregate;

/// <summary>Aggregate root representing a user authorized to access the Proxy Manager.</summary>
public sealed class AuthorizedUser
{
    private AuthorizedUser() { }

    /// <summary>Authentik subject identifier (opaque string, used as the public key).</summary>
    public string Sub { get; private set; } = string.Empty;

    /// <summary>User's full display name.</summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>User's preferred short name for UI display.</summary>
    public string Nickname { get; private set; } = string.Empty;

    /// <summary>User's email address.</summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>Optional absolute HTTP/HTTPS URL of the user's profile image.</summary>
    public string? ProfileImageUrl { get; private set; }

    /// <summary>The permission level granted to this user.</summary>
    public UserAccessLevel AccessLevel { get; private set; }

    /// <summary>Current lifecycle status of the user account.</summary>
    public UserStatus Status { get; private set; }

    /// <summary>UTC timestamp when the user was first authorized.</summary>
    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>UTC timestamp of the last modification to this record.</summary>
    public DateTimeOffset LastModifiedAt { get; private set; }

    /// <summary>UTC timestamp when the user was deactivated, if applicable.</summary>
    public DateTimeOffset? DeactivatedAt { get; private set; }

    /// <summary>
    /// Creates a new active authorized user. Validates all required fields and the optional
    /// <paramref name="profileImageUrl"/> before constructing the instance.
    /// </summary>
    /// <param name="sub">Authentik subject identifier (required).</param>
    /// <param name="displayName">Full display name (required).</param>
    /// <param name="nickname">Short preferred name for UI display (required).</param>
    /// <param name="email">Email address (required).</param>
    /// <param name="profileImageUrl">Optional absolute HTTP/HTTPS profile image URL.</param>
    /// <param name="accessLevel">Permission level to assign.</param>
    /// <param name="actorSub">Subject of the user performing this action (for audit).</param>
    /// <exception cref="UserValidationException">Thrown when any required field is blank or the URL is invalid.</exception>
    public static AuthorizedUser Create(
        string sub,
        string displayName,
        string nickname,
        string email,
        string? profileImageUrl,
        UserAccessLevel accessLevel,
        string actorSub)
    {
        if (string.IsNullOrWhiteSpace(sub))
            throw new UserValidationException("Sub is required.");
        if (string.IsNullOrWhiteSpace(displayName))
            throw new UserValidationException("DisplayName is required.");
        if (string.IsNullOrWhiteSpace(nickname))
            throw new UserValidationException("Nickname is required.");
        if (string.IsNullOrWhiteSpace(email))
            throw new UserValidationException("Email is required.");

        if (profileImageUrl is not null && !IsValidProfileImageUrl(profileImageUrl))
            throw new UserValidationException($"ProfileImageUrl must be an absolute http or https URI. Got: '{profileImageUrl}'.");

        var now = DateTimeOffset.UtcNow;
        return new AuthorizedUser
        {
            Sub = sub,
            DisplayName = displayName,
            Nickname = nickname,
            Email = email,
            ProfileImageUrl = profileImageUrl,
            AccessLevel = accessLevel,
            Status = UserStatus.Active,
            CreatedAt = now,
            LastModifiedAt = now
        };
    }

    /// <summary>Soft-deletes the user by setting status to Deactivated and recording the timestamp.</summary>
    public void Deactivate()
    {
        var now = DateTimeOffset.UtcNow;
        Status = UserStatus.Deactivated;
        DeactivatedAt = now;
        LastModifiedAt = now;
    }

    /// <summary>Restores a deactivated user to active status with the given access level.</summary>
    /// <param name="accessLevel">The access level to assign on reactivation.</param>
    public void Reactivate(UserAccessLevel accessLevel)
    {
        Status = UserStatus.Active;
        AccessLevel = accessLevel;
        DeactivatedAt = null;
        LastModifiedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Updates the user's access level and records the modification timestamp.</summary>
    /// <param name="accessLevel">The new access level to assign.</param>
    public void UpdateAccessLevel(UserAccessLevel accessLevel)
    {
        AccessLevel = accessLevel;
        LastModifiedAt = DateTimeOffset.UtcNow;
    }

    private static bool IsValidProfileImageUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
