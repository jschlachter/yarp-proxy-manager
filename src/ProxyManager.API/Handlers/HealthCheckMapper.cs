using West94.ProxyManager.Core.AggregatesModel.ProxyHostAggregate;
using West94.ProxyManager.Core.DTOs;
using West94.ProxyManager.Core.Exceptions;

namespace West94.ProxyManager.API.Handlers;

/// <summary>Maps health check settings between the wire DTO and the domain (ADR 0003).</summary>
internal static class HealthCheckMapper
{
    /// <summary>
    /// Converts the wire DTO to domain settings. Returns null when the DTO is null or both checks are
    /// off. Invalid values surface as <see cref="ProxyHostValidationException"/> (400).
    /// </summary>
    public static HealthCheckSettings? ToDomain(HealthCheckDto? dto)
    {
        if (dto is null || (dto.Active is null && dto.Passive is null))
            return null;

        var available = ParseEnum<AvailableDestinationsPolicy>(dto.AvailableDestinationsPolicy, "healthCheck.availableDestinationsPolicy");

        ActiveHealthCheck? active = null;
        if (dto.Active is { } a)
        {
            var policy = ParseEnum<ActiveHealthCheckPolicy>(a.Policy, "healthCheck.active.policy");
            active = Validate("healthCheck.active", () => new ActiveHealthCheck(
                policy,
                Seconds(a.IntervalSeconds),
                Seconds(a.TimeoutSeconds),
                a.Path,
                a.Query,
                a.HealthAddress,
                a.ConsecutiveFailuresThreshold));
        }

        PassiveHealthCheck? passive = null;
        if (dto.Passive is { } p)
        {
            var policy = ParseEnum<PassiveHealthCheckPolicy>(p.Policy, "healthCheck.passive.policy");
            passive = Validate("healthCheck.passive", () => new PassiveHealthCheck(
                policy,
                Seconds(p.ReactivationPeriodSeconds),
                p.FailureRateLimit));
        }

        return new HealthCheckSettings(active, passive, available);
    }

    /// <summary>Converts domain settings to the wire DTO; null stays null.</summary>
    public static HealthCheckDto? ToDto(HealthCheckSettings? settings) => settings is null ? null : new(
        settings.AvailableDestinationsPolicy.ToString(),
        settings.Active is { } a
            ? new ActiveHealthCheckDto(
                a.Policy.ToString(),
                WholeSeconds(a.Interval),
                WholeSeconds(a.Timeout),
                a.Path,
                a.Query,
                a.HealthAddress,
                a.ConsecutiveFailuresThreshold)
            : null,
        settings.Passive is { } p
            ? new PassiveHealthCheckDto(p.Policy.ToString(), WholeSeconds(p.ReactivationPeriod), p.FailureRateLimit)
            : null);

    private static TEnum ParseEnum<TEnum>(string? value, string field) where TEnum : struct, Enum
    {
        // IsDefined rejects numeric strings such as "7" that TryParse would otherwise accept.
        if (Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
            return parsed;

        throw new ProxyHostValidationException(
            $"'{value}' is not a valid value for '{field}'. Use one of: {string.Join(", ", Enum.GetNames<TEnum>())}.");
    }

    private static T Validate<T>(string field, Func<T> create)
    {
        try
        {
            return create();
        }
        catch (ArgumentException ex)
        {
            throw new ProxyHostValidationException($"Invalid '{field}.{ex.ParamName}': {ex.Message}");
        }
    }

    private static TimeSpan? Seconds(int? seconds) => seconds is { } s ? TimeSpan.FromSeconds(s) : null;

    private static int? WholeSeconds(TimeSpan? value) => value is { } v ? (int)v.TotalSeconds : null;
}
