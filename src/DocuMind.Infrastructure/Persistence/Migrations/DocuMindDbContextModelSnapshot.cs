using System;
using DocuMind.Core.Documents;
using DocuMind.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Pgvector;

#nullable disable

namespace DocuMind.Infrastructure.Persistence.Migrations;

[DbContext(typeof(DocuMindDbContext))]
partial class DocuMindDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder
            .HasDefaultSchema("public");

        modelBuilder.Entity("DocuMind.Infrastructure.Persistence.Entities.DocumentChunkEntity", builder =>
        {
            builder.Property<Guid>("Id")
                .HasColumnType("uuid");

            builder.Property<int>("CharacterCount")
                .HasColumnType("integer");

            builder.Property<string>("Content")
                .IsRequired()
                .HasColumnType("text");

            builder.Property<Guid>("DocumentId")
                .HasColumnType("uuid");

            builder.Property<Vector>("Embedding")
                .HasColumnType("vector(1536)");

            builder.Property<int>("Order")
                .HasColumnType("integer");

            builder.Property<string>("PageLabel")
                .HasColumnType("text");

            builder.Property<int?>("TokenCount")
                .HasColumnType("integer");

            builder.HasKey("Id");

            builder.HasIndex("DocumentId", "Order")
                .IsUnique();

            builder.ToTable("document_chunks", "public");
        });

        modelBuilder.Entity("DocuMind.Infrastructure.Persistence.Entities.DocumentEntity", builder =>
        {
            builder.Property<Guid>("Id")
                .HasColumnType("uuid");

            builder.Property<string>("Checksum")
                .HasMaxLength(256)
                .HasColumnType("character varying(256)");

            builder.Property<string>("ContentType")
                .IsRequired()
                .HasMaxLength(256)
                .HasColumnType("character varying(256)");

            builder.Property<string>("FailureReason")
                .HasMaxLength(2048)
                .HasColumnType("character varying(2048)");

            builder.Property<string>("FileName")
                .IsRequired()
                .HasMaxLength(512)
                .HasColumnType("character varying(512)");

            builder.Property<long>("SizeInBytes")
                .HasColumnType("bigint");

            builder.Property<int>("Status")
                .HasColumnType("integer");

            builder.Property<DateTimeOffset>("UpdatedAtUtc")
                .HasColumnType("timestamp with time zone");

            builder.Property<DateTimeOffset>("UploadedAtUtc")
                .HasColumnType("timestamp with time zone");

            builder.HasKey("Id");

            builder.ToTable("documents", "public");
        });

        modelBuilder.Entity("DocuMind.Infrastructure.Persistence.Entities.DocumentChunkEntity", builder =>
        {
            builder.HasOne("DocuMind.Infrastructure.Persistence.Entities.DocumentEntity", "Document")
                .WithMany("Chunks")
                .HasForeignKey("DocumentId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            builder.Navigation("Document");
        });

        modelBuilder.Entity("DocuMind.Infrastructure.Persistence.Entities.DocumentEntity", builder =>
        {
            builder.Navigation("Chunks");
        });
#pragma warning restore 612, 618
    }
}
