using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobApplicationAgent.Collection.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectionOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "collection_outbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    SchemaVersion = table.Column<int>(type: "integer", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    DispatchedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_collection_outbox", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_collection_outbox_OccurredAtUtc_Id",
                table: "collection_outbox",
                columns: new[] { "OccurredAtUtc", "Id" },
                filter: "\"DispatchedAtUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_collection_outbox_RevisionId",
                table: "collection_outbox",
                column: "RevisionId",
                unique: true);
            // Backfill acquisition facts, including already reviewed revisions, without changing their decisions.
            migrationBuilder.Sql("""
                INSERT INTO collection_outbox
                    ("Id", "RevisionId", "EventType", "SchemaVersion", "OccurredAtUtc", "PayloadJson")
                SELECT event_id, "Id", 'collection.offer-revision-recorded.v1', 1, "CollectedAtUtc",
                    jsonb_build_object(
                        'eventId', event_id, 'eventType', 'collection.offer-revision-recorded.v1', 'schemaVersion', 1,
                        'occurredAtUtc', to_char("CollectedAtUtc" AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.US"Z"'),
                        'producer', 'Collection', 'offerId', "OfferId", 'revisionId', "Id",
                        'fingerprint', "Fingerprint", 'normalizationVersion', "NormalizationVersion",
                        'sourceName', "ContentJson" ->> 'SourceName')
                FROM (SELECT gen_random_uuid() AS event_id, r.* FROM offer_revisions r) existing;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "collection_outbox");
        }
    }
}
