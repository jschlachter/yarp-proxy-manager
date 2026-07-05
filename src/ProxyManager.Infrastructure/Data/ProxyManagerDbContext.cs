using Microsoft.EntityFrameworkCore;
using West94.ProxyManager.Core.AggregatesModel.AuditLogAggregate;

namespace West94.ProxyManager.Infrastructure.Data;

public sealed class ProxyManagerDbContext(DbContextOptions<ProxyManagerDbContext> options)
    : DbContext(options)
{
    internal DbSet<ProxyHostRecord> ProxyHosts => Set<ProxyHostRecord>();
    internal DbSet<CertificateRecord> Certificates => Set<CertificateRecord>();
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProxyManagerDbContext).Assembly);
    }
}
