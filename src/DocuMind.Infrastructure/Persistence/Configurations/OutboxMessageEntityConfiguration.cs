using DocuMind.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DocuMind.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageEntityConfiguration : IEntityTypeConfiguration<OutboxMessageEntity>
{
    public void Configure(EntityTypeBuilder<OutboxMessageEntity> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(message => message.Id);
        builder.Property(message => message.Id)
            .ValueGeneratedNever();

        builder.Property(message => message.Type)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(message => message.DocumentId)
            .IsRequired();

        builder.Property(message => message.Payload)
            .IsRequired();

        builder.Property(message => message.OccurredAtUtc)
            .IsRequired();

        builder.Property(message => message.Error)
            .HasMaxLength(2048);

        builder.HasIndex(message => message.ProcessedAtUtc);
        builder.HasIndex(message => new { message.DocumentId, message.Type });
    }
}
