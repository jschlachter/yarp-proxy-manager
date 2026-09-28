using West94.ProxyManager.Core.AggregatesModel.ProxyHostAggregate;

namespace West94.ProxyManager.Core.Tests.Unit;

[Trait("Category", "Unit")]
public class HealthCheckSettingsTests
{
    private static readonly PassiveHealthCheck Passive = new(PassiveHealthCheckPolicy.TransportFailureRate);

    [Fact]
    public void Settings_WithBothChecksNull_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new HealthCheckSettings(null, null, AvailableDestinationsPolicy.HealthyAndUnknown));
    }

    [Fact]
    public void Settings_WithOneCheck_IsAccepted()
    {
        var settings = new HealthCheckSettings(null, Passive, AvailableDestinationsPolicy.HealthyOrPanic);

        Assert.Same(Passive, settings.Passive);
        Assert.Null(settings.Active);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Active_NonPositiveInterval_Throws(int seconds)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ActiveHealthCheck(ActiveHealthCheckPolicy.ConsecutiveFailures, interval: TimeSpan.FromSeconds(seconds)));
    }

    [Fact]
    public void Active_NonPositiveTimeout_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ActiveHealthCheck(ActiveHealthCheckPolicy.ConsecutiveFailures, timeout: TimeSpan.Zero));
    }

    [Fact]
    public void Active_ThresholdBelowOne_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ActiveHealthCheck(ActiveHealthCheckPolicy.ConsecutiveFailures, consecutiveFailuresThreshold: 0));
    }

    [Fact]
    public void Active_PathWithoutLeadingSlash_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new ActiveHealthCheck(ActiveHealthCheckPolicy.ConsecutiveFailures, path: "health"));
    }

    [Fact]
    public void Active_QueryWithoutLeadingQuestionMark_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new ActiveHealthCheck(ActiveHealthCheckPolicy.ConsecutiveFailures, query: "a=1"));
    }

    [Theory]
    [InlineData("backend:8080")]
    [InlineData("/relative")]
    [InlineData("ftp://backend")]
    public void Active_InvalidHealthAddress_Throws(string address)
    {
        Assert.Throws<ArgumentException>(() =>
            new ActiveHealthCheck(ActiveHealthCheckPolicy.ConsecutiveFailures, healthAddress: address));
    }

    [Fact]
    public void Active_BoundaryValues_AreAccepted()
    {
        var active = new ActiveHealthCheck(
            ActiveHealthCheckPolicy.ConsecutiveFailures,
            interval: TimeSpan.FromSeconds(1),
            timeout: TimeSpan.FromSeconds(1),
            path: "/",
            query: "?",
            healthAddress: "https://backend:8443",
            consecutiveFailuresThreshold: 1);

        Assert.Equal(1, active.ConsecutiveFailuresThreshold);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    [InlineData(-0.1)]
    public void Passive_RateLimitOutsideOpenInterval_Throws(double rate)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PassiveHealthCheck(PassiveHealthCheckPolicy.TransportFailureRate, failureRateLimit: rate));
    }

    [Fact]
    public void Passive_ValidValues_AreAccepted()
    {
        var passive = new PassiveHealthCheck(PassiveHealthCheckPolicy.TransportFailureRate, TimeSpan.FromSeconds(1), 0.5);

        Assert.Equal(0.5, passive.FailureRateLimit);
    }

    [Fact]
    public void Passive_NonPositiveReactivationPeriod_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PassiveHealthCheck(PassiveHealthCheckPolicy.TransportFailureRate, reactivationPeriod: TimeSpan.Zero));
    }

    [Fact]
    public void ConfigureHealthCheck_Null_ClearsSettings()
    {
        var host = ProxyHost.Create(
            ["example.com"],
            DestinationUri.Parse("http://backend:8080"),
            healthCheck: new HealthCheckSettings(null, Passive, AvailableDestinationsPolicy.HealthyAndUnknown));

        host.ConfigureHealthCheck(null);

        Assert.Null(host.HealthCheck);
    }
}
