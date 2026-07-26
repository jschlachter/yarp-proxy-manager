namespace West94.ProxyManager.Core.AggregatesModel.CertificateAggregate;

/// <summary>X509 metadata extracted from the uploaded certificate bytes at creation time.</summary>
public sealed record CertificateSubjectInfo(
    string Subject,
    IReadOnlyList<string> SubjectAlternativeNames,
    DateTimeOffset NotBefore,
    DateTimeOffset NotAfter,
    string Thumbprint);
