using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobApplicationAgent.Documents.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentExports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_document_compositions_DraftId_OwnerId",
                table: "document_compositions");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_document_compositions_DraftId_OwnerId",
                table: "document_compositions",
                columns: new[] { "DraftId", "OwnerId" });

            migrationBuilder.CreateTable(
                name: "document_exports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    DraftId = table.Column<Guid>(type: "uuid", nullable: false),
                    Format = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    RendererVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CompositionSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FileSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FileName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MediaType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Content = table.Column<byte[]>(type: "bytea", nullable: false),
                    Length = table.Column<int>(type: "integer", nullable: false),
                    PageCount = table.Column<int>(type: "integer", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ApprovedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_exports", x => x.Id);
                    table.CheckConstraint("CK_document_export_length", "octet_length(\"Content\") BETWEEN 1 AND 5242880 AND \"Length\" = octet_length(\"Content\")");
                    table.ForeignKey(
                        name: "FK_document_exports_document_compositions_DraftId_OwnerId",
                        columns: x => new { x.DraftId, x.OwnerId },
                        principalTable: "document_compositions",
                        principalColumns: new[] { "DraftId", "OwnerId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_document_exports_DraftId_Format",
                table: "document_exports",
                columns: new[] { "DraftId", "Format" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_document_exports_DraftId_OwnerId",
                table: "document_exports",
                columns: new[] { "DraftId", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_document_exports_OwnerId_CreatedAtUtc_Id",
                table: "document_exports",
                columns: new[] { "OwnerId", "CreatedAtUtc", "Id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_exports");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_document_compositions_DraftId_OwnerId",
                table: "document_compositions");

            migrationBuilder.CreateIndex(
                name: "IX_document_compositions_DraftId_OwnerId",
                table: "document_compositions",
                columns: new[] { "DraftId", "OwnerId" });
        }
    }
}
