namespace West94.ProxyManager.Core.Messages.Commands;

/// <summary>Permanently deletes a proxy host by ID.</summary>
public sealed record DeleteProxyHostCommand(Guid Id, string ActorId);
