namespace West94.ProxyManager.Core.Exceptions;

/// <summary>Thrown when a proxy host creation would conflict with an existing domain name.</summary>
public sealed class ProxyHostConflictException(string domainName)
    : Exception($"A proxy host with domain name '{domainName}' already exists.");
