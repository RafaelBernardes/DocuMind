using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace DocuMind.Infrastructure.Persistence.Migrations;

[DbContext(typeof(DocuMindDbContext))]
[Migration("20260429230000_InitialCreate")]
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS vector;");
        migrationBuilder.EnsureSchema(
            name: "public");

        migrationBuilder.CreateTable(
            name: "documents",
            schema: "public",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                FileName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                ContentType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                SizeInBytes = table.Column<long>(type: "bigint", nullable: false),
                Checksum = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                Status = table.Column<int>(type: "integer", nullable: false),
                UploadedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                FailureReason = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_documents", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "document_chunks",
            schema: "public",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                Order = table.Column<int>(type: "integer", nullable: false),
                Content = table.Column<string>(type: "text", nullable: false),
                CharacterCount = table.Column<int>(type: "integer", nullable: false),
                TokenCount = table.Column<int>(type: "integer", nullable: true),
                PageLabel = table.Column<string>(type: "text", nullable: true),
                Embedding = table.Column<Vector>(type: "vector(1536)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_document_chunks", x => x.Id);
                table.ForeignKey(
                    name: "FK_document_chunks_documents_DocumentId",
                    column: x => x.DocumentId,
                    principalSchema: "public",
                    principalTable: "documents",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_document_chunks_DocumentId_Order",
            schema: "public",
            table: "document_chunks",
            columns: new[] { "DocumentId", "Order" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "document_chunks",
            schema: "public");

        migrationBuilder.DropTable(
            name: "documents",
            schema: "public");
    }
}
