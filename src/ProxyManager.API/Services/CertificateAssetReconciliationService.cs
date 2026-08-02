using West94.ProxyManager.API.Infrastructure.Files;
using West94.ProxyManager.Core.AggregatesModel.CertificateAggregate;

namespace West94.ProxyManager.API.Services;

/// <summary>
/// Startup-only reconciliation pass covering the one real gap in the create-certificate flow: a
/// crash or Files outage between the DB write and the commit calls leaves a certificate row
/// pointing at assets still <c>Staged</c>. Commit is idempotent on the Files side, so re-driving
/// it here is always safe. No extra scheduling needed — the sweeper's owner-less/staged-only rule
/// already protects these rows from being swept.
/// </summary>
public sealed class CertificateAssetReconciliationService(
    IServiceScopeFactory scopeFactory, ILogger<CertificateAssetReconciliationService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var certificates = scope.ServiceProvider.GetRequiredService<ICertificateRepository>();
        var files = scope.ServiceProvider.GetRequiredService<IFileAssetClient>();

        var all = await certificates.GetAllAsync(ct);
        foreach (var cert in all)
        {
            await ReconcileAsync(files, cert.CertificateAssetId, cert.Id, ct);
            if (cert.KeyAssetId is { } keyAssetId)
            {
                await ReconcileAsync(files, keyAssetId, cert.Id, ct);
            }
        }
    }

    private async Task ReconcileAsync(IFileAssetClient files, Guid assetId, Guid certificateId, CancellationToken ct)
    {
        try
        {
            var asset = await files.GetAsync(assetId, ct);
            if (asset is { Status: "Staged" })
            {
                logger.LogWarning(
                    "Re-driving commit for asset {AssetId} still Staged for certificate {CertificateId}.",
                    assetId, certificateId);
                await files.CommitAsync(assetId, "certificate", certificateId, ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Reconciliation check failed for asset {AssetId} (certificate {CertificateId}); will retry next restart.",
                assetId, certificateId);
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
