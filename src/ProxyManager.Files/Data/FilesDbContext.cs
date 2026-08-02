using Microsoft.EntityFrameworkCore;

namespace West94.ProxyManager.Files.Data;

/// <summary>
/// Owns the "files" Postgres schema exclusively. Runs against the same Postgres instance as
/// ProxyManagerDbContext but MUST use its own schema and migrations history table
/// (configured at <see cref="OnConfiguring"/>-adjacent registration time, not here) —
/// otherwise the two services corrupt each other's migration history.
/// </summary>
public sealed class FilesDbContext(DbContextOptions<FilesDbContext> options) : DbContext(options)
{
    internal DbSet<FileAssetRecord> FileAssets => Set<FileAssetRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("files");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FilesDbContext).Assembly);
    }
}
