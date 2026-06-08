using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using Microsoft.IdentityModel.Tokens;

namespace West94.ProxyManager.API.Tests.Helpers;

/// <summary>
/// Generates self-signed JWT bearer tokens for integration tests.
/// The TestWebAppFactory configures the API to accept tokens signed with this factory's key.
/// </summary>
public static class TestJwtFactory
{
    public const string TestIssuer = "https://test-issuer";
    public const string TestAudience = "test-audience";

    // Minimum 32-byte key required for HMAC-SHA256
    private static readonly SymmetricSecurityKey SigningKey =
        new(Encoding.UTF8.GetBytes("test-signing-key-that-is-at-least-32-bytes-long!"));

    public static SecurityKey GetSigningKey() => SigningKey;

    /// <summary>Creates a signed JWT with the given subject claim.</summary>
    public static string CreateToken(string sub = "test-user")
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, sub),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: TestIssuer,
            audience: TestAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>Creates a signed JWT with the given subject and Proxy Manager role claim.</summary>
    /// <param name="sub">The subject identifier (Authentik sub claim value).</param>
    /// <param name="pmRole">The Proxy Manager role claim value: "Admin" or "ReadOnly".</param>
    public static string CreateToken(string sub, string pmRole)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, sub),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("pm_role", pmRole)
        };

        var token = new JwtSecurityToken(
            issuer: TestIssuer,
            audience: TestAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
