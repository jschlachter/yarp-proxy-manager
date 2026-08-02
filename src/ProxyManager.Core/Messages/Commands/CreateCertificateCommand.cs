namespace West94.ProxyManager.Core.Messages.Commands;

/// <summary>Creates a new certificate record from already-uploaded (Staged) Files assets.</summary>
public sealed record CreateCertificateCommand(
    string Name,
    string Format,
    Guid CertificateAssetId,
    Guid? KeyAssetId,
    string? PassPhrase,
    string ActorId);
