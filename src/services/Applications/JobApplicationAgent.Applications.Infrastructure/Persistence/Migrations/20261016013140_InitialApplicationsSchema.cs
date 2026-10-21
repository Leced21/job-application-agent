using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobApplicationAgent.Applications.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialApplicationsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Candidacies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CompanyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    HistoryJson = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Candidacies", x => x.Id);
                    table.UniqueConstraint("AK_Candidacies_OwnerId_Id", x => new { x.OwnerId, x.Id });
                    table.CheckConstraint("CK_Candidacy_Dates", "\"UpdatedAtUtc\" >= \"CreatedAtUtc\"");
                    table.CheckConstraint("CK_Candidacy_History", "octet_length(\"HistoryJson\") <= 16777216");
                    table.CheckConstraint("CK_Candidacy_Ids", "\"OwnerId\" <> '00000000-0000-0000-0000-000000000000' AND \"ProfileId\" <> '00000000-0000-0000-0000-000000000000' AND \"JobId\" <> '00000000-0000-0000-0000-000000000000'");
                    table.CheckConstraint("CK_Candidacy_State", "\"Status\" IN (1,2,3,4) AND \"Revision\" BETWEEN 1 AND 1000");
                });

            migrationBuilder.CreateTable(
                name: "CandidacyRequests",
                columns: table => new
                {
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidacyId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidacyRequests", x => new { x.OwnerId, x.RequestId });
                    table.ForeignKey(
                        name: "FK_CandidacyRequests_Candidacies_OwnerId_CandidacyId",
                        columns: x => new { x.OwnerId, x.CandidacyId },
                        principalTable: "Candidacies",
                        principalColumns: new[] { "OwnerId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Candidacies_OwnerId_CreatedAtUtc_Id",
                table: "Candidacies",
                columns: new[] { "OwnerId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Candidacies_OwnerId_ProfileId_JobId",
                table: "Candidacies",
                columns: new[] { "OwnerId", "ProfileId", "JobId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CandidacyRequests_OwnerId_CandidacyId",
                table: "CandidacyRequests",
                columns: new[] { "OwnerId", "CandidacyId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CandidacyRequests");

            migrationBuilder.DropTable(
                name: "Candidacies");
        }
    }
}
