using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobApplicationAgent.Profile.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "candidate_preferences",
                columns: table => new
                {
                    CandidateProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    DesiredJobTitles = table.Column<string[]>(type: "text[]", nullable: false),
                    PreferredLocations = table.Column<string[]>(type: "text[]", nullable: false),
                    ContractTypes = table.Column<string[]>(type: "text[]", nullable: false),
                    WorkModes = table.Column<string[]>(type: "text[]", nullable: false),
                    MinimumAnnualGrossSalary = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    SalaryCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    AvailableFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_preferences", x => x.CandidateProfileId);
                    table.ForeignKey(
                        name: "FK_candidate_preferences_candidate_profiles_CandidateProfileId",
                        column: x => x.CandidateProfileId,
                        principalTable: "candidate_profiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "candidate_preferences");
        }
    }
}
