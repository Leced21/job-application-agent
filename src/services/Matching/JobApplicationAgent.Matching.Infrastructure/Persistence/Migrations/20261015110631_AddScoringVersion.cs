using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobApplicationAgent.Matching.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddScoringVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ScoringVersion",
                table: "matching_results",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "legacy-v1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ScoringVersion",
                table: "matching_results");
        }
    }
}
