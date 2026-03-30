namespace West94.ProxyManager.Core.DTOs;

/// <summary>Certificate information exposed in API responses (password excluded).</summary>
public sealed record ProxyCertificateDto(string CertificatePath, string? KeyPath);

/// <summary>Read model for a proxy host returned by query handlers and API endpoints.</summary>
public sealed record ProxyHostDto(
    Guid Id,
    IReadOnlyList<string> DomainNames,
    string Destination,
    bool IsEnabled,
    ProxyCertificateDto? Certificate);
