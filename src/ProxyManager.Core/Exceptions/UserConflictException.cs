namespace West94.ProxyManager.Core.Exceptions;

/// <summary>Thrown when attempting to create a user whose subject identifier is already active.</summary>
public sealed class UserConflictException(string sub)
    : Exception($"A user with sub '{sub}' is already active.");
