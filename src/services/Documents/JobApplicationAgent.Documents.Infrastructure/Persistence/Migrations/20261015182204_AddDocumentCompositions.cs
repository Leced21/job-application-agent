using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobApplicationAgent.Documents.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentCompositions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_prepared_document_drafts_Id_OwnerId",
                table: "prepared_document_drafts",
                columns: new[] { "Id", "OwnerId" });

            migrationBuilder.CreateTable(
                name: "document_compositions",
                columns: table => new
                {
                    DraftId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TemplateVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SourceSnapshotSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ContentSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ContentJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Decision = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ReviewedByOwnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_compositions", x => x.DraftId);
                    table.CheckConstraint("CK_composition_content_size", "octet_length(\"ContentJson\") BETWEEN 1 AND 2097152");
                    table.ForeignKey(
                        name: "FK_document_compositions_prepared_document_drafts_DraftId_Owne~",
                        columns: x => new { x.DraftId, x.OwnerId },
                        principalTable: "prepared_document_drafts",
                        principalColumns: new[] { "Id", "OwnerId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_document_compositions_DraftId_OwnerId",
                table: "document_compositions",
                columns: new[] { "DraftId", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_document_compositions_OwnerId_CreatedAtUtc_DraftId",
                table: "document_compositions",
                columns: new[] { "OwnerId", "CreatedAtUtc", "DraftId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_compositions");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_prepared_document_drafts_Id_OwnerId",
                table: "prepared_document_drafts");
        }
    }
}
