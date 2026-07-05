namespace West94.ProxyManager.Core.Messages.Queries;

/// <summary>Returns a paginated list of all certificates, sorted by name.</summary>
public sealed record GetCertificatesQuery(int Page = 1, int PageSize = 20);
