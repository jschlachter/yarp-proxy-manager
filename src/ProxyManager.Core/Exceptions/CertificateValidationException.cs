namespace West94.ProxyManager.Core.Exceptions;

/// <summary>Thrown when certificate input fails domain validation.</summary>
public sealed class CertificateValidationException(string message) : Exception(message);
