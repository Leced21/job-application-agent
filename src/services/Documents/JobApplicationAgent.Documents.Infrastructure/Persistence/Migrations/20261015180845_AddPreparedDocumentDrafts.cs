using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobApplicationAgent.Documents.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPreparedDocumentDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "prepared_document_drafts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResumeId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SnapshotSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SnapshotJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prepared_document_drafts", x => x.Id);
                    table.CheckConstraint("CK_prepared_draft_snapshot_size", "octet_length(\"SnapshotJson\") BETWEEN 1 AND 2097152");
                    table.ForeignKey(
                        name: "FK_prepared_document_drafts_resume_documents_ResumeId_OwnerId",
                        columns: x => new { x.ResumeId, x.OwnerId },
                        principalTable: "resume_documents",
                        principalColumns: new[] { "Id", "OwnerId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_prepared_document_drafts_OwnerId_CreatedAtUtc_Id",
                table: "prepared_document_drafts",
                columns: new[] { "OwnerId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_prepared_document_drafts_OwnerId_RequestId",
                table: "prepared_document_drafts",
                columns: new[] { "OwnerId", "RequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_prepared_document_drafts_ResumeId_OwnerId",
                table: "prepared_document_drafts",
                columns: new[] { "ResumeId", "OwnerId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "prepared_document_drafts");
        }
    }
}
