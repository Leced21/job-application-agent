using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobApplicationAgent.Documents.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddResumeSeries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_resume_documents_Id_OwnerId",
                table: "resume_documents",
                columns: new[] { "Id", "OwnerId" });

            migrationBuilder.CreateTable(
                name: "resume_series",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resume_series", x => x.Id);
                    table.UniqueConstraint("AK_resume_series_Id_OwnerId", x => new { x.Id, x.OwnerId });
                });

            migrationBuilder.CreateTable(
                name: "resume_series_versions",
                columns: table => new
                {
                    ResumeId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeriesId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    Label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AddedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resume_series_versions", x => x.ResumeId);
                    table.CheckConstraint("CK_resume_series_version_number", "\"VersionNumber\" BETWEEN 1 AND 1000");
                    table.ForeignKey(
                        name: "FK_resume_series_versions_resume_documents_ResumeId_OwnerId",
                        columns: x => new { x.ResumeId, x.OwnerId },
                        principalTable: "resume_documents",
                        principalColumns: new[] { "Id", "OwnerId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_resume_series_versions_resume_series_SeriesId_OwnerId",
                        columns: x => new { x.SeriesId, x.OwnerId },
                        principalTable: "resume_series",
                        principalColumns: new[] { "Id", "OwnerId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_resume_series_OwnerId_CreatedAtUtc_Id",
                table: "resume_series",
                columns: new[] { "OwnerId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_resume_series_versions_ResumeId_OwnerId",
                table: "resume_series_versions",
                columns: new[] { "ResumeId", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_resume_series_versions_SeriesId_OwnerId",
                table: "resume_series_versions",
                columns: new[] { "SeriesId", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_resume_series_versions_SeriesId_VersionNumber",
                table: "resume_series_versions",
                columns: new[] { "SeriesId", "VersionNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "resume_series_versions");

            migrationBuilder.DropTable(
                name: "resume_series");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_resume_documents_Id_OwnerId",
                table: "resume_documents");
        }
    }
}
