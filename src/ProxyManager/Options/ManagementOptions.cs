namespace West94.ProxyManager.Options;

/// <summary>
/// The host name(s) the proxy's own UI, management API and account endpoints answer on (ADR 0005).
/// Every other domain belongs to user routes, so system routes and endpoints are scoped to these hosts.
/// </summary>
public sealed class ManagementOptions
{
    public const string Section = "Management";

    public string[] Hosts { get; set; } = [];
}

public static class ManagementOptionsExtensions
{
    /// <summary>
    /// Binds <see cref="ManagementOptions"/> and fails startup when no management host is set: without
    /// one, host-less system routes would be scoped to nothing and the UI would be unreachable.
    /// </summary>
    public static IServiceCollection AddManagementOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ManagementOptions>()
            .Bind(configuration.GetSection(ManagementOptions.Section))
            .Validate(o => o.Hosts.Length > 0 && o.Hosts.All(h => !string.IsNullOrWhiteSpace(h)),
                $"{ManagementOptions.Section}:{nameof(ManagementOptions.Hosts)} must list at least one host name.")
            .ValidateOnStart();

        return services;
    }
}
