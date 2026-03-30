namespace West94.ProxyManager.Core.Messages.Queries;

/// <summary>Returns the full configuration of a single proxy host by its unique identifier.</summary>
public sealed record GetProxyHostByIdQuery(Guid Id);
