namespace West94.ProxyManager.Core.DTOs;

/// <summary>Read model for a certificate returned by query handlers and API endpoints. PassPhrase is intentionally excluded.</summary>
public sealed record CertificateDto(
    Guid Id,
    string Name,
    string Format,
    Guid CertificateAssetId,
    Guid? KeyAssetId,
    string CertificateFileName,
    string? KeyFileName,
    string Subject,
    IReadOnlyList<string> SubjectAlternativeNames,
    DateTimeOffset NotBefore,
    DateTimeOffset NotAfter,
    string Thumbprint,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
