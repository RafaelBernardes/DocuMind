using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocuMind.Infrastructure.Persistence.Migrations;

[DbContext(typeof(DocuMindDbContext))]
[Migration("20260513120000_AddOutboxMessages")]
public partial class AddOutboxMessages : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "outbox_messages",
            schema: "public",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Type = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                Payload = table.Column<string>(type: "text", nullable: false),
                OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ProcessedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                Error = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_outbox_messages", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_outbox_messages_DocumentId_Type",
            schema: "public",
            table: "outbox_messages",
            columns: new[] { "DocumentId", "Type" });

        migrationBuilder.CreateIndex(
            name: "IX_outbox_messages_ProcessedAtUtc",
            schema: "public",
            table: "outbox_messages",
            column: "ProcessedAtUtc");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "outbox_messages",
            schema: "public");
    }
}
