using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DocuMind.Infrastructure.Persistence.Migrations;

public partial class AddDocumentRecoveryMetadata : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "FailureCategory",
            schema: "public",
            table: "documents",
            type: "integer",
            nullable: true);

        migrationBuilder.Sql("""
            UPDATE public.documents
            SET "FailureCategory" = 2
            WHERE "Status" = 3 AND "FailureCategory" IS NULL;
            """);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "LastProcessingStartedAtUtc",
            schema: "public",
            table: "documents",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "LastRecoveryPointAtUtc",
            schema: "public",
            table: "documents",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "ProcessingAttemptCount",
            schema: "public",
            table: "documents",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "RecoveryPoint",
            schema: "public",
            table: "documents",
            type: "integer",
            nullable: false,
            defaultValue: 0);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "FailureCategory",
            schema: "public",
            table: "documents");

        migrationBuilder.DropColumn(
            name: "LastProcessingStartedAtUtc",
            schema: "public",
            table: "documents");

        migrationBuilder.DropColumn(
            name: "LastRecoveryPointAtUtc",
            schema: "public",
            table: "documents");

        migrationBuilder.DropColumn(
            name: "ProcessingAttemptCount",
            schema: "public",
            table: "documents");

        migrationBuilder.DropColumn(
            name: "RecoveryPoint",
            schema: "public",
            table: "documents");
    }
}
