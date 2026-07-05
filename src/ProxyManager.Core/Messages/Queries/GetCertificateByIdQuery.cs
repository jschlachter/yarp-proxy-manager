namespace West94.ProxyManager.Core.Messages.Queries;

/// <summary>Returns a single certificate by its id, or null if not found.</summary>
public sealed record GetCertificateByIdQuery(Guid Id);
