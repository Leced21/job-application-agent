using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobApplicationAgent.Applications.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddManualApplicationFollowUp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Candidacy_State",
                table: "Candidacies");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Candidacy_State",
                table: "Candidacies",
                sql: "\"Status\" IN (1,2,3,4,5,6,7,8,9) AND \"Revision\" BETWEEN 1 AND 1000");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                  IF EXISTS (SELECT 1 FROM "Candidacies" WHERE "Status" > 4
                    OR "HistoryJson"::jsonb @> '[{"Kind":"ManualSubmissionDeclared"}]'::jsonb
                    OR "HistoryJson"::jsonb @> '[{"Kind":"RecruiterUpdateRecorded"}]'::jsonb) THEN
                    RAISE EXCEPTION 'Cannot downgrade: manual follow-up history requires this schema and application version.';
                  END IF;
                END $$;
                """);
            migrationBuilder.DropCheckConstraint(
                name: "CK_Candidacy_State",
                table: "Candidacies");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Candidacy_State",
                table: "Candidacies",
                sql: "\"Status\" IN (1,2,3,4) AND \"Revision\" BETWEEN 1 AND 1000");
        }
    }
}
