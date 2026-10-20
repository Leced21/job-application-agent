using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobApplicationAgent.Applications.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationFollowUpReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NextFollowUpChannel",
                table: "Candidacies",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextFollowUpDueAtUtc",
                table: "Candidacies",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "NextFollowUpId",
                table: "Candidacies",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NextFollowUpNote",
                table: "Candidacies",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Candidacies_OwnerId_NextFollowUpDueAtUtc_Id",
                table: "Candidacies",
                columns: new[] { "OwnerId", "NextFollowUpDueAtUtc", "Id" },
                filter: "\"NextFollowUpId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Candidacy_FollowUp",
                table: "Candidacies",
                sql: "(\"NextFollowUpId\" IS NULL AND \"NextFollowUpDueAtUtc\" IS NULL AND \"NextFollowUpChannel\" IS NULL AND \"NextFollowUpNote\" IS NULL)\nOR (\"NextFollowUpId\" IS NOT NULL AND \"NextFollowUpId\" <> '00000000-0000-0000-0000-000000000000'\n  AND \"NextFollowUpDueAtUtc\" IS NOT NULL AND \"NextFollowUpChannel\" IS NOT NULL AND \"NextFollowUpChannel\" BETWEEN 1 AND 6\n  AND \"NextFollowUpNote\" IS NOT NULL AND char_length(\"NextFollowUpNote\") BETWEEN 1 AND 1000 AND \"Status\" IN (5,6,7))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM "Candidacies" c WHERE EXISTS (
                    SELECT 1 FROM jsonb_array_elements(c."HistoryJson"::jsonb) e
                    WHERE e->'FollowUp' IS NOT NULL AND e->'FollowUp' <> 'null'::jsonb)) THEN
                    RAISE EXCEPTION 'Cannot downgrade: follow-up reminder history requires this application version.';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropIndex(
                name: "IX_Candidacies_OwnerId_NextFollowUpDueAtUtc_Id",
                table: "Candidacies");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Candidacy_FollowUp",
                table: "Candidacies");

            migrationBuilder.DropColumn(
                name: "NextFollowUpChannel",
                table: "Candidacies");

            migrationBuilder.DropColumn(
                name: "NextFollowUpDueAtUtc",
                table: "Candidacies");

            migrationBuilder.DropColumn(
                name: "NextFollowUpId",
                table: "Candidacies");

            migrationBuilder.DropColumn(
                name: "NextFollowUpNote",
                table: "Candidacies");
        }
    }
}
