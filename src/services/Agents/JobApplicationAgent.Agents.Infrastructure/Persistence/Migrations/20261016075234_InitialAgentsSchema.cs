using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobApplicationAgent.Agents.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialAgentsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agent_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    Snapshot = table.Column<string>(type: "text", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReservedTokens = table.Column<long>(type: "bigint", nullable: true),
                    ReservedUsd = table.Column<decimal>(type: "numeric", nullable: true),
                    ChargedTokens = table.Column<long>(type: "bigint", nullable: false),
                    ChargedUsd = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_runs", x => x.Id);
                    table.CheckConstraint("ck_agent_charge", "\"ChargedTokens\" >= 0 AND \"ChargedUsd\" >= 0");
                    table.CheckConstraint("ck_agent_reservation", "(\"ReservedTokens\" IS NULL AND \"ReservedUsd\" IS NULL) OR (\"ReservedTokens\" IS NOT NULL AND \"ReservedUsd\" IS NOT NULL AND \"ReservedTokens\" >= 0 AND \"ReservedUsd\" >= 0 AND \"State\" = 1)");
                });

            migrationBuilder.CreateTable(
                name: "agent_audits",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    Metadata = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_audits", x => new { x.RunId, x.Revision });
                    table.ForeignKey(
                        name: "FK_agent_audits_agent_runs_RunId",
                        column: x => x.RunId,
                        principalTable: "agent_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agent_runs_OwnerId_ExpiresAtUtc",
                table: "agent_runs",
                columns: new[] { "OwnerId", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_agent_runs_OwnerId_RequestId",
                table: "agent_runs",
                columns: new[] { "OwnerId", "RequestId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM agent_runs) THEN
                        RAISE EXCEPTION 'Agent history/accounting exists. Export and explicitly retire the database before removing this schema.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropTable(
                name: "agent_audits");

            migrationBuilder.DropTable(
                name: "agent_runs");
        }
    }
}
