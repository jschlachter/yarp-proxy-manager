namespace West94.ProxyManager.Core.Messages.Commands;

/// <summary>
/// Updates the mutable metadata of an existing certificate.
/// CertificatePath and KeyFilePath are immutable after creation — replace via delete + recreate.
/// </summary>
public sealed record UpdateCertificateCommand(
    Guid Id,
    string? Name,
    string? PassPhrase,
    string ActorId);
