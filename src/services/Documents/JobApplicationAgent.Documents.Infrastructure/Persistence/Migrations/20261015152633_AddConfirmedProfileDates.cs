using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobApplicationAgent.Documents.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddConfirmedProfileDates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ProfileDatesConfirmedAtUtc",
                table: "resume_candidate_facts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProfileDatesConfirmedByOwnerId",
                table: "resume_candidate_facts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfileDatesJson",
                table: "resume_candidate_facts",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProfileDatesConfirmedAtUtc",
                table: "resume_candidate_facts");

            migrationBuilder.DropColumn(
                name: "ProfileDatesConfirmedByOwnerId",
                table: "resume_candidate_facts");

            migrationBuilder.DropColumn(
                name: "ProfileDatesJson",
                table: "resume_candidate_facts");
        }
    }
}
