using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using West94.ProxyManager.Core.AggregatesModel.ProxyHostAggregate;

namespace West94.ProxyManager.Infrastructure.Data.Configurations;

internal sealed class ProxyHostConfiguration : IEntityTypeConfiguration<ProxyHostRecord>
{
    public void Configure(EntityTypeBuilder<ProxyHostRecord> builder)
    {
        builder.ToTable("proxy_hosts");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.DomainNames)
            .HasColumnName("domain_names")
            .HasColumnType("text[]")
            .IsRequired();

        builder.Property(x => x.DestinationScheme)
            .HasColumnName("destination_scheme")
            .HasMaxLength(5)
            .IsRequired();

        builder.Property(x => x.DestinationHost)
            .HasColumnName("destination_host")
            .HasMaxLength(253)
            .IsRequired();

        builder.Property(x => x.DestinationPort)
            .HasColumnName("destination_port")
            .IsRequired();

        builder.Property(x => x.IsEnabled)
            .HasColumnName("is_enabled")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(x => x.CertificateId)
            .HasColumnName("certificate_id");

        builder.Property(x => x.TlsMode)
            .HasColumnName("tls_mode")
            .IsRequired()
            .HasDefaultValue(TlsMode.Manual);

        // Settings are always read and written as a whole and never queried, so one jsonb column
        // keeps the mapping small (ADR 0003). Records compare by value, so the default comparer works.
        builder.Property(x => x.HealthCheck)
            .HasColumnName("health_check")
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonSerializerOptions.Default),
                v => JsonSerializer.Deserialize<HealthCheckRecord>(v, JsonSerializerOptions.Default));
    }
}
