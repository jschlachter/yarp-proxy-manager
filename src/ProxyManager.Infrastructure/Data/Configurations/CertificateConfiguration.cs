using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace West94.ProxyManager.Infrastructure.Data.Configurations;

internal sealed class CertificateConfiguration : IEntityTypeConfiguration<CertificateRecord>
{
    public void Configure(EntityTypeBuilder<CertificateRecord> builder)
    {
        builder.ToTable("certificates");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.Format)
            .HasColumnName("format")
            .IsRequired();

        builder.Property(x => x.CertificatePath)
            .HasColumnName("certificate_path")
            .IsRequired();

        builder.Property(x => x.KeyFilePath)
            .HasColumnName("key_file_path");

        builder.Property(x => x.PassPhrase)
            .HasColumnName("pass_phrase");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();
    }
}
