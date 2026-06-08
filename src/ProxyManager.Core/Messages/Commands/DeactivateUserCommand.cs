namespace West94.ProxyManager.Core.Messages.Commands;

/// <summary>Soft-deletes an active user, revoking their access without destroying the record.</summary>
public sealed record DeactivateUserCommand(
    string Sub,
    string ActorSub);
