using DocuMind.Infrastructure.Persistence.Entities;
using DocuMind.Core.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocuMind.Infrastructure.Persistence.Configurations;

public sealed class DocumentChunkEntityConfiguration : IEntityTypeConfiguration<DocumentChunkEntity>
{
    public void Configure(EntityTypeBuilder<DocumentChunkEntity> builder)
    {
        builder.ToTable("document_chunks");

        builder.HasKey(chunk => chunk.Id);
        builder.Property(chunk => chunk.Id)
            .ValueGeneratedNever();

        builder.Property(chunk => chunk.Content)
            .IsRequired();

        builder.Property(chunk => chunk.Order)
            .IsRequired();

        builder.Property(chunk => chunk.CharacterCount)
            .IsRequired();

        builder.Property(chunk => chunk.Embedding)
            .HasColumnType($"vector({EmbeddingConstants.ExpectedDimensions})");

        builder.HasIndex(chunk => new { chunk.DocumentId, chunk.Order })
            .IsUnique();
    }
}
