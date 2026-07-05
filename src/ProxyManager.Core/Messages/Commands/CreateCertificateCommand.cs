namespace West94.ProxyManager.Core.Messages.Commands;

/// <summary>Creates a new certificate record for use with proxy hosts.</summary>
public sealed record CreateCertificateCommand(
    string Name,
    string Format,
    string CertificatePath,
    string? KeyFilePath,
    string? PassPhrase,
    string ActorId);
