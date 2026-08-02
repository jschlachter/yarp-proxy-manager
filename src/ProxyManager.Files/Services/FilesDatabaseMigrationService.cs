using Microsoft.EntityFrameworkCore;
using West94.ProxyManager.Files.Data;

namespace West94.ProxyManager.Files.Services;

/// <summary>Applies EF Core migrations against the "files" schema at application startup before the service begins accepting requests.</summary>
public sealed class FilesDatabaseMigrationService(
    IServiceScopeFactory scopeFactory,
    ILogger<FilesDatabaseMigrationService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Applying files-schema database migrations...");

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FilesDbContext>();

        await db.Database.MigrateAsync(cancellationToken);

        logger.LogInformation("Files-schema database migrations applied successfully.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
