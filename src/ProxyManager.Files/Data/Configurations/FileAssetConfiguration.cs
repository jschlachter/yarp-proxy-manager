using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace West94.ProxyManager.Files.Data.Configurations;

internal sealed class FileAssetConfiguration : IEntityTypeConfiguration<FileAssetRecord>
{
    public void Configure(EntityTypeBuilder<FileAssetRecord> builder)
    {
        builder.ToTable("file_assets");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");

        builder.Property(x => x.AssetType)
            .HasColumnName("asset_type")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.FileName)
            .HasColumnName("file_name")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.ContentType)
            .HasColumnName("content_type")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.SizeBytes)
            .HasColumnName("size_bytes")
            .IsRequired();

        builder.Property(x => x.Sha256)
            .HasColumnName("sha256")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.StorageKey)
            .HasColumnName("storage_key")
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .IsRequired();

        builder.Property(x => x.OwnerType)
            .HasColumnName("owner_type")
            .HasMaxLength(64);

        builder.Property(x => x.OwnerId)
            .HasColumnName("owner_id");

        builder.Property(x => x.UploadedBy)
            .HasColumnName("uploaded_by")
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.CommittedAt)
            .HasColumnName("committed_at");

        builder.HasIndex(x => new { x.OwnerType, x.OwnerId });
        builder.HasIndex(x => new { x.Status, x.CreatedAt });
    }
}
