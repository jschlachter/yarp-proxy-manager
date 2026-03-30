namespace West94.ProxyManager.Core.Messages.Commands;

/// <summary>Creates a new proxy host mapping public domain names to an upstream backend.</summary>
public sealed record CreateProxyHostCommand(
    IEnumerable<string> DomainNames,
    string DestinationUri,
    string? CertificatePath,
    string? CertificateKeyPath,
    string ActorId);
