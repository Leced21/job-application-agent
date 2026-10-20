using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobApplicationAgent.Documents.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentApplicationBindings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_document_exports_Id_OwnerId",
                table: "document_exports",
                columns: new[] { "Id", "OwnerId" });

            migrationBuilder.CreateTable(
                name: "document_application_bindings",
                columns: table => new
                {
                    ExportId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    DraftId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FileSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CompositionSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SnapshotSha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_application_bindings", x => x.ExportId);
                    table.ForeignKey(
                        name: "FK_document_application_bindings_document_exports_ExportId_Own~",
                        columns: x => new { x.ExportId, x.OwnerId },
                        principalTable: "document_exports",
                        principalColumns: new[] { "Id", "OwnerId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_document_application_bindings_ExportId_OwnerId",
                table: "document_application_bindings",
                columns: new[] { "ExportId", "OwnerId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_application_bindings");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_document_exports_Id_OwnerId",
                table: "document_exports");
        }
    }
}
