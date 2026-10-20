using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobApplicationAgent.Collection.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOfferRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "offer_revisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OfferId = table.Column<Guid>(type: "uuid", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OriginalTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContentJson = table.Column<string>(type: "jsonb", nullable: false),
                    NormalizationVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    CollectedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Decision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReviewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_offer_revisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_offer_revisions_collected_offers_OfferId",
                        column: x => x.OfferId,
                        principalTable: "collected_offers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_offer_revisions_OfferId_CollectedAtUtc_Id",
                table: "offer_revisions",
                columns: new[] { "OfferId", "CollectedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_offer_revisions_OfferId_Fingerprint",
                table: "offer_revisions",
                columns: new[] { "OfferId", "Fingerprint" },
                unique: true);
            // Preserve the original snapshot of imports created before revision tracking existed.
            // Previously observed changed bodies were not stored and cannot be reconstructed.
            migrationBuilder.Sql("""
                INSERT INTO offer_revisions
                    ("Id", "OfferId", "Fingerprint", "OriginalTitle", "ContentJson", "NormalizationVersion", "CollectedAtUtc", "Decision")
                SELECT gen_random_uuid(), "Id", "ContentFingerprint", "OriginalTitle",
                    jsonb_build_object(
                        'Title', "Title", 'CompanyName', "CompanyName", 'Location', "Location",
                        'WorkMode', "WorkMode", 'ContractType', "ContractType", 'SalaryMin', "SalaryMin",
                        'SalaryMax', "SalaryMax", 'SalaryCurrency', "SalaryCurrency", 'Description', "Description",
                        'Skills', "Skills", 'SourceName', "SourceName", 'SourceUrl', "SourceUrl",
                        'PublishedAtUtc', to_char("PublishedAtUtc" AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.US"Z"'),
                        'ExpiresAtUtc', to_char("ExpiresAtUtc" AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.US"Z"')),
                    "NormalizationVersion", "FirstCollectedAtUtc", 'Pending'
                FROM collected_offers;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "offer_revisions");
        }
    }
}
