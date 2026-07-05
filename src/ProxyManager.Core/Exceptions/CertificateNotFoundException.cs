namespace West94.ProxyManager.Core.Exceptions;

/// <summary>Thrown when a certificate with the given id does not exist.</summary>
public sealed class CertificateNotFoundException(Guid id)
    : Exception($"No certificate with id '{id}' was found.");
