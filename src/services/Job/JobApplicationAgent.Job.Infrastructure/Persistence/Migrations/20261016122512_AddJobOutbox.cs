using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobApplicationAgent.Job.Infrastructure.Persistence.Migrations;

public partial class AddJobOutbox : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "job_outbox",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                JobId = table.Column<Guid>(type: "uuid", nullable: false),
                EventType = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                SchemaVersion = table.Column<int>(type: "integer", nullable: false),
                OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                DispatchedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                AttemptCount = table.Column<int>(type: "integer", nullable: false),
                NextAttemptAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastFailureCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_job_outbox", x => x.Id));

        migrationBuilder.CreateIndex("IX_job_outbox_OccurredAtUtc_Id", "job_outbox", new[] { "OccurredAtUtc", "Id" },
            filter: "\"DispatchedAtUtc\" IS NULL");
        migrationBuilder.CreateIndex("IX_job_outbox_NextAttemptAtUtc_OccurredAtUtc_Id", "job_outbox",
            new[] { "NextAttemptAtUtc", "OccurredAtUtc", "Id" }, filter: "\"DispatchedAtUtc\" IS NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable("job_outbox");
}
