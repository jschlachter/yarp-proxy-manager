namespace West94.ProxyManager.Core.Messages.Commands;

/// <summary>Deletes a certificate record and removes its files from disk.</summary>
public sealed record DeleteCertificateCommand(Guid Id, string ActorId);
