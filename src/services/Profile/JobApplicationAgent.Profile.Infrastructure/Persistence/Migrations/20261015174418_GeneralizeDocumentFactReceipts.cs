using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobApplicationAgent.Profile.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GeneralizeDocumentFactReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SkillId",
                table: "document_fact_imports",
                newName: "EntityId");

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "document_fact_imports",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Skill");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM document_fact_imports WHERE "Kind" <> 'Skill') THEN
                        RAISE EXCEPTION 'Cannot downgrade document receipts containing dated categories.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropColumn(
                name: "Kind",
                table: "document_fact_imports");

            migrationBuilder.RenameColumn(
                name: "EntityId",
                table: "document_fact_imports",
                newName: "SkillId");
        }
    }
}
