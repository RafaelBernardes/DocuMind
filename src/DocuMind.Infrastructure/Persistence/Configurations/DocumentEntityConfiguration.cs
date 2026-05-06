using DocuMind.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocuMind.Infrastructure.Persistence.Configurations;

public sealed class DocumentEntityConfiguration : IEntityTypeConfiguration<DocumentEntity>
{
    public void Configure(EntityTypeBuilder<DocumentEntity> builder)
    {
        builder.ToTable("documents");

        builder.HasKey(document => document.Id);
        builder.Property(document => document.Id)
            .ValueGeneratedNever();

        builder.Property(document => document.FileName)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(document => document.ContentType)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(document => document.Checksum)
            .HasMaxLength(256);

        builder.Property(document => document.StorageRelativePath)
            .HasMaxLength(1024);

        builder.Property(document => document.Status)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(document => document.UploadedAtUtc)
            .IsRequired();

        builder.Property(document => document.UpdatedAtUtc)
            .IsRequired();

        builder.Property(document => document.FailureReason)
            .HasMaxLength(2048);

        builder.HasMany(document => document.Chunks)
            .WithOne(chunk => chunk.Document)
            .HasForeignKey(chunk => chunk.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
