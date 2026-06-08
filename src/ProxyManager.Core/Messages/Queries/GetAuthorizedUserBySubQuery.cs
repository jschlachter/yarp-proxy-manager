namespace West94.ProxyManager.Core.Messages.Queries;

/// <summary>Returns a single authorized user by their Authentik subject identifier.</summary>
public sealed record GetAuthorizedUserBySubQuery(string Sub);
