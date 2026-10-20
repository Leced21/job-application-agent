using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobApplicationAgent.Profile.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProfileOwner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "owner_id",
                table: "candidate_profiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_candidate_profiles_owner_id",
                table: "candidate_profiles",
                column: "owner_id",
                unique: true,
                filter: "owner_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_candidate_profiles_owner_id",
                table: "candidate_profiles");

            migrationBuilder.DropColumn(
                name: "owner_id",
                table: "candidate_profiles");
        }
    }
}
