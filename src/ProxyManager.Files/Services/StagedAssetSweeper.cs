using Microsoft.Extensions.Options;

using West94.ProxyManager.Files.Options;
using West94.ProxyManager.Files.Repositories;
using West94.ProxyManager.Files.Storage;

namespace West94.ProxyManager.Files.Services;

/// <summary>
/// Backstop for the two-phase upload: deletes <c>Staged</c> assets (and their blobs) that are
/// older than <see cref="UploadOptions.StagingTtl"/> and still have no owner — covers uploads that
/// were never committed (abandoned by the client, or the process died between upload and commit).
/// </summary>
public sealed class StagedAssetSweeper(
    IServiceScopeFactory scopeFactory,
    IOptions<UploadOptions> options,
    ILogger<StagedAssetSweeper> logger) : BackgroundService
{
    private readonly UploadOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_options.SweepInterval);
        do
        {
            await SweepOnceAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SweepOnceAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IFileAssetRepository>();
        var objectStore = scope.ServiceProvider.GetRequiredService<IObjectStore>();

        var swept = await StagedAssetSweepRunner.SweepAsync(repository, objectStore, _options.StagingTtl, ct);
        if (swept > 0)
        {
            logger.LogInformation("Swept {Count} expired staged asset(s).", swept);
        }
    }
}
