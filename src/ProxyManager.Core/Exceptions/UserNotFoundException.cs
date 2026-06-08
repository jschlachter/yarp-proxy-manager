namespace West94.ProxyManager.Core.Exceptions;

/// <summary>Thrown when no active user with the specified subject identifier exists.</summary>
public sealed class UserNotFoundException(string sub)
    : Exception($"No active user with sub '{sub}' was found.");
