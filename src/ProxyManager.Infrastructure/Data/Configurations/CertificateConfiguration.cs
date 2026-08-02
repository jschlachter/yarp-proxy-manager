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

        builder.Property(x => x.CertificateAssetId)
            .HasColumnName("certificate_asset_id")
            .IsRequired();

        builder.Property(x => x.KeyAssetId)
            .HasColumnName("key_asset_id");

        builder.Property(x => x.CertificateFileName)
            .HasColumnName("certificate_file_name")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.KeyFileName)
            .HasColumnName("key_file_name")
            .HasMaxLength(256);

        builder.Property(x => x.PassPhrase)
            .HasColumnName("pass_phrase");

        builder.Property(x => x.Subject)
            .HasColumnName("subject")
            .IsRequired();

        builder.Property(x => x.SubjectAlternativeNames)
            .HasColumnName("subject_alternative_names")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(x => x.NotBefore)
            .HasColumnName("not_before")
            .IsRequired();

        builder.Property(x => x.NotAfter)
            .HasColumnName("not_after")
            .IsRequired();

        builder.Property(x => x.Thumbprint)
            .HasColumnName("thumbprint")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();
    }
}
