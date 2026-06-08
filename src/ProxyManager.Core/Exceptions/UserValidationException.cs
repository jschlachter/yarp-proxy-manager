namespace West94.ProxyManager.Core.Exceptions;

/// <summary>Thrown when a user command contains invalid or missing field values.</summary>
public sealed class UserValidationException(string message) : Exception(message);
