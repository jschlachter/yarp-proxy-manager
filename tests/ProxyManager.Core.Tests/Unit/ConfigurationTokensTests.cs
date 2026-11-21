using West94.ProxyManager.Core.Configuration;

namespace West94.ProxyManager.Core.Tests.Unit;

[Trait("Category", "Unit")]
public class ConfigurationTokensTests
{
    private static readonly Dictionary<string, string> Values = new()
    {
        ["POSTGRES_PASSWORD"] = "s3cr+t/=",
        ["DB_HOST"] = "proxymanager-postgresql",
    };

    private static string? Lookup(string key) => Values.GetValueOrDefault(key);

    [Fact]
    public void Interpolate_ReplacesAllKnownTokens()
    {
        var result = ConfigurationTokens.Interpolate(
            "Host={{DB_HOST}};Password={{POSTGRES_PASSWORD}}", Lookup, out var missing);

        Assert.Equal("Host=proxymanager-postgresql;Password=s3cr+t/=", result);
        Assert.Empty(missing);
    }

    [Fact]
    public void Interpolate_WithoutTokens_ReturnsInputUnchanged()
    {
        const string plain = "Host=localhost;Password=password123!";

        var result = ConfigurationTokens.Interpolate(plain, Lookup, out var missing);

        Assert.Equal(plain, result);
        Assert.Empty(missing);
    }

    [Fact]
    public void Interpolate_UnknownToken_IsLeftInPlaceAndReportedOnce()
    {
        var result = ConfigurationTokens.Interpolate(
            "User={{DB_USER}};Password={{POSTGRES_PASSWORD}};Again={{DB_USER}}", Lookup, out var missing);

        Assert.Equal("User={{DB_USER}};Password=s3cr+t/=;Again={{DB_USER}}", result);
        Assert.Equal(["DB_USER"], missing);
    }

    [Fact]
    public void Interpolate_EmptyValue_CountsAsResolved()
    {
        var result = ConfigurationTokens.Interpolate("Password={{EMPTY}}", _ => string.Empty, out var missing);

        Assert.Equal("Password=", result);
        Assert.Empty(missing);
    }

    [Theory]
    [InlineData("{{has-dash}}")]
    [InlineData("{{ SPACED }}")]
    [InlineData("{POSTGRES_PASSWORD}")]
    public void Interpolate_MalformedPlaceholder_IsNotATokenAndIsLeftAlone(string template)
    {
        var result = ConfigurationTokens.Interpolate(template, Lookup, out var missing);

        Assert.Equal(template, result);
        Assert.Empty(missing);
    }
}
