namespace West94.ProxyManager.Core.Messages.Commands;

/// <summary>
/// Updates an existing proxy host. Only non-null fields are applied;
/// absent fields leave the existing configuration unchanged.
/// </summary>
public sealed record UpdateProxyHostCommand(
    Guid Id,
    IEnumerable<string>? DomainNames,
    string? DestinationUri,
    bool? IsEnabled,
    string? CertificatePath,
    string? CertificateKeyPath,
    string ActorId);
