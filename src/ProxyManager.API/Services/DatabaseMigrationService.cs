using Microsoft.EntityFrameworkCore;
using West94.ProxyManager.Infrastructure.Data;

namespace West94.ProxyManager.API.Services;

/// <summary>Applies EF Core migrations at application startup before the service begins accepting requests.</summary>
public sealed class DatabaseMigrationService(
    IServiceScopeFactory scopeFactory,
    ILogger<DatabaseMigrationService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Applying database migrations...");

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProxyManagerDbContext>();

        await db.Database.MigrateAsync(cancellationToken);

        logger.LogInformation("Database migrations applied successfully.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
