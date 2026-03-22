namespace West94.ProxyManager.Core.Messages.Queries;

/// <summary>Returns a paginated list of all proxy hosts, sorted by first domain name ascending.</summary>
public sealed record GetProxyHostsQuery(int Page = 1, int PageSize = 20);
