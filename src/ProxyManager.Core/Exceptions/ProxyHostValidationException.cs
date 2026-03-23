namespace West94.ProxyManager.Core.Exceptions;

/// <summary>Thrown when a proxy host command contains invalid field values.</summary>
public sealed class ProxyHostValidationException(string message) : Exception(message);
