namespace West94.ProxyManager.Core.AggregatesModel.ProxyHostAggregate;

public sealed record ProxyCertificate(
    string CertificatePath,
    string? KeyPath = default,
    string? Password = default);
