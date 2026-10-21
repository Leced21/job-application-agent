using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobApplicationAgent.Collection.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectionOutboxDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "collection_outbox",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LastFailureCode",
                table: "collection_outbox",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextAttemptAtUtc",
                table: "collection_outbox",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_collection_outbox_NextAttemptAtUtc_OccurredAtUtc_Id",
                table: "collection_outbox",
                columns: new[] { "NextAttemptAtUtc", "OccurredAtUtc", "Id" },
                filter: "\"DispatchedAtUtc\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_collection_outbox_NextAttemptAtUtc_OccurredAtUtc_Id",
                table: "collection_outbox");

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "collection_outbox");

            migrationBuilder.DropColumn(
                name: "LastFailureCode",
                table: "collection_outbox");

            migrationBuilder.DropColumn(
                name: "NextAttemptAtUtc",
                table: "collection_outbox");
        }
    }
}
