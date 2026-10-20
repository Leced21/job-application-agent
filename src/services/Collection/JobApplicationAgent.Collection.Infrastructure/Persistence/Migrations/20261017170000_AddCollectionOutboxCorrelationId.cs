using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobApplicationAgent.Collection.Infrastructure.Persistence.Migrations;

public partial class AddCollectionOutboxCorrelationId : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.AddColumn<string>(
        name: "CorrelationId", table: "collection_outbox", type: "character varying(36)", maxLength: 36, nullable: true);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropColumn(
        name: "CorrelationId", table: "collection_outbox");
}
