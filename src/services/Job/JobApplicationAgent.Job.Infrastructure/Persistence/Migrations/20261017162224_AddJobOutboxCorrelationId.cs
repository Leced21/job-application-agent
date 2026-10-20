using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobApplicationAgent.Job.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddJobOutboxCorrelationId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                table: "job_outbox",
                type: "character varying(36)",
                maxLength: 36,
                nullable: true);
        }
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "CorrelationId", table: "job_outbox");
        }
    }
}
