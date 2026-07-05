using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

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
    }
}
