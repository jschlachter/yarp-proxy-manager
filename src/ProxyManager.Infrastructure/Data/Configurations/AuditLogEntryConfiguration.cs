using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using West94.ProxyManager.Core.AggregatesModel.AuditLogAggregate;

namespace West94.ProxyManager.Infrastructure.Data.Configurations;

internal sealed class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.ToTable("audit_log_entries");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ActorId)
            .HasColumnName("actor_id")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.Operation)
            .HasColumnName("operation")
            .IsRequired();

        builder.Property(x => x.ProxyHostId)
            .HasColumnName("proxy_host_id")
            .IsRequired();

        builder.Property(x => x.PreviousState)
            .HasColumnName("previous_state");

        builder.Property(x => x.NewState)
            .HasColumnName("new_state");

        builder.Property(x => x.OccurredAt)
            .HasColumnName("occurred_at")
            .IsRequired();

        builder.HasIndex(x => x.OccurredAt)
            .HasDatabaseName("ix_audit_log_occurred_at");
    }
}
