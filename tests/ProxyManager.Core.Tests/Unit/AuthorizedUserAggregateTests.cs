using West94.ProxyManager.Core.AggregatesModel.UserAggregate;
using West94.ProxyManager.Core.Exceptions;

namespace West94.ProxyManager.Core.Tests.Unit;

public class AuthorizedUserAggregateTests
{
    private static AuthorizedUser MakeUser(
        string sub = "sub|123",
        string displayName = "Alice Smith",
        string nickname = "Alice",
        string email = "alice@example.com",
        string? profileImageUrl = null,
        UserAccessLevel accessLevel = UserAccessLevel.ReadOnly) =>
        AuthorizedUser.Create(sub, displayName, nickname, email, profileImageUrl, accessLevel, "actor|001");

    // ── Create ──────────────────────────────────────────────────────────────

    [Fact]
    public void Create_WithValidArgs_SetsAllFieldsCorrectly()
    {
        var before = DateTimeOffset.UtcNow;
        var user = MakeUser(profileImageUrl: "https://example.com/avatar.png");

        Assert.Equal("sub|123", user.Sub);
        Assert.Equal("Alice Smith", user.DisplayName);
        Assert.Equal("Alice", user.Nickname);
        Assert.Equal("alice@example.com", user.Email);
        Assert.Equal("https://example.com/avatar.png", user.ProfileImageUrl);
        Assert.Equal(UserAccessLevel.ReadOnly, user.AccessLevel);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Null(user.DeactivatedAt);
        Assert.True(user.CreatedAt >= before);
        Assert.True(user.LastModifiedAt >= before);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankSub_ThrowsUserValidationException(string sub)
    {
        Assert.Throws<UserValidationException>(() => MakeUser(sub: sub));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankDisplayName_ThrowsUserValidationException(string displayName)
    {
        Assert.Throws<UserValidationException>(() => MakeUser(displayName: displayName));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankNickname_ThrowsUserValidationException(string nickname)
    {
        Assert.Throws<UserValidationException>(() => MakeUser(nickname: nickname));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankEmail_ThrowsUserValidationException(string email)
    {
        Assert.Throws<UserValidationException>(() => MakeUser(email: email));
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://invalid-scheme.com")]
    [InlineData("/relative/path")]
    public void Create_WithInvalidProfileImageUrl_ThrowsUserValidationException(string url)
    {
        Assert.Throws<UserValidationException>(() => MakeUser(profileImageUrl: url));
    }

    [Fact]
    public void Create_WithNullProfileImageUrl_Succeeds()
    {
        var user = MakeUser(profileImageUrl: null);
        Assert.Null(user.ProfileImageUrl);
    }

    [Theory]
    [InlineData("https://cdn.example.com/img.png")]
    [InlineData("http://localhost/avatar")]
    public void Create_WithValidProfileImageUrl_Succeeds(string url)
    {
        var user = MakeUser(profileImageUrl: url);
        Assert.Equal(url, user.ProfileImageUrl);
    }

    // ── Deactivate ──────────────────────────────────────────────────────────

    [Fact]
    public void Deactivate_ActiveUser_SetsStatusDeactivatedAndStampsTimestamp()
    {
        var user = MakeUser();
        var before = DateTimeOffset.UtcNow;

        user.Deactivate();

        Assert.Equal(UserStatus.Deactivated, user.Status);
        Assert.NotNull(user.DeactivatedAt);
        Assert.True(user.DeactivatedAt >= before);
        Assert.True(user.LastModifiedAt >= before);
    }

    // ── Reactivate ──────────────────────────────────────────────────────────

    [Fact]
    public void Reactivate_DeactivatedUser_ClearsDeactivatedAtAndSetsStatusActive()
    {
        var user = MakeUser();
        user.Deactivate();

        user.Reactivate(UserAccessLevel.Admin);

        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Null(user.DeactivatedAt);
        Assert.Equal(UserAccessLevel.Admin, user.AccessLevel);
    }

    // ── UpdateAccessLevel ───────────────────────────────────────────────────

    [Fact]
    public void UpdateAccessLevel_ChangesAccessLevelAndUpdatesLastModified()
    {
        var user = MakeUser(accessLevel: UserAccessLevel.ReadOnly);
        var before = DateTimeOffset.UtcNow;

        user.UpdateAccessLevel(UserAccessLevel.Admin);

        Assert.Equal(UserAccessLevel.Admin, user.AccessLevel);
        Assert.True(user.LastModifiedAt >= before);
    }
}
