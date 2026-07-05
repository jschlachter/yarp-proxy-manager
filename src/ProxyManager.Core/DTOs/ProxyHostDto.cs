namespace West94.ProxyManager.Core.DTOs;

/// <summary>Read model for a proxy host returned by query handlers and API endpoints.</summary>
public sealed record ProxyHostDto(
    Guid Id,
    IReadOnlyList<string> DomainNames,
    string Destination,
    bool IsEnabled,
    Guid? CertificateId);
