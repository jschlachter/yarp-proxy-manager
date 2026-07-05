namespace West94.ProxyManager.Core.Messages.Commands;

/// <summary>Assigns or unassigns a certificate on a proxy host. Pass null for CertificateId to unassign.</summary>
public sealed record AssignCertificateCommand(
    Guid ProxyHostId,
    Guid? CertificateId,
    string ActorId);
