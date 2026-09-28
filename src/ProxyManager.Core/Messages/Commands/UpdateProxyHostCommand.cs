using West94.ProxyManager.Core.DTOs;

namespace West94.ProxyManager.Core.Messages.Commands;

/// <summary>
/// Updates an existing proxy host. Only non-null fields are applied;
/// absent fields leave the existing configuration unchanged. A <c>HealthCheck</c> with both
/// checks null clears the health checks.
/// </summary>
public sealed record UpdateProxyHostCommand(
    Guid Id,
    IEnumerable<string>? DomainNames,
    string? DestinationUri,
    bool? IsEnabled,
    string ActorId,
    string? TlsMode = null,
    HealthCheckDto? HealthCheck = null);
