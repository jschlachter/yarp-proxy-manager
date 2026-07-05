namespace West94.ProxyManager.Core.DTOs;

/// <summary>Read model for a certificate returned by query handlers and API endpoints. PassPhrase is intentionally excluded.</summary>
public sealed record CertificateDto(
    Guid Id,
    string Name,
    string Format,
    string CertificatePath,
    string? KeyFilePath,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
