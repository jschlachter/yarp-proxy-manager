namespace West94.ProxyManager.Core.Exceptions;

/// <summary>Thrown when a proxy host with the given id does not exist.</summary>
public sealed class ProxyHostNotFoundException(Guid id)
    : Exception($"No proxy host with id '{id}' was found.");
