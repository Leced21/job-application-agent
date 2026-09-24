using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JobApplicationAgent.Job.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialJobSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_job_offers_CreatedAtUtc_Id",
                table: "job_offers");

            migrationBuilder.RenameColumn(
                name: "Title",
                table: "job_offers",
                newName: "title");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "job_offers",
                newName: "status");

            migrationBuilder.RenameColumn(
                name: "Source",
                table: "job_offers",
                newName: "source");

            migrationBuilder.RenameColumn(
                name: "Location",
                table: "job_offers",
                newName: "location");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "job_offers",
                newName: "description");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "job_offers",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "WorkMode",
                table: "job_offers",
                newName: "work_mode");

            migrationBuilder.RenameColumn(
                name: "UpdatedAtUtc",
                table: "job_offers",
                newName: "updated_at_utc");

            migrationBuilder.RenameColumn(
                name: "SourceUrl",
                table: "job_offers",
                newName: "source_url");

            migrationBuilder.RenameColumn(
                name: "SalaryMin",
                table: "job_offers",
                newName: "salary_min");

            migrationBuilder.RenameColumn(
                name: "SalaryMax",
                table: "job_offers",
                newName: "salary_max");

            migrationBuilder.RenameColumn(
                name: "SalaryCurrency",
                table: "job_offers",
                newName: "salary_currency");

            migrationBuilder.RenameColumn(
                name: "PublishedAtUtc",
                table: "job_offers",
                newName: "published_at_utc");

            migrationBuilder.RenameColumn(
                name: "CreatedAtUtc",
                table: "job_offers",
                newName: "created_at_utc");

            migrationBuilder.RenameColumn(
                name: "ContractType",
                table: "job_offers",
                newName: "contract_type");

            migrationBuilder.RenameColumn(
                name: "CompanyName",
                table: "job_offers",
                newName: "company_name");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "job_offers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "work_mode",
                table: "job_offers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "contract_type",
                table: "job_offers",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.CreateIndex(
                name: "IX_job_offers_company_name",
                table: "job_offers",
                column: "company_name");

            migrationBuilder.CreateIndex(
                name: "IX_job_offers_published_at_utc",
                table: "job_offers",
                column: "published_at_utc");

            migrationBuilder.CreateIndex(
                name: "IX_job_offers_status",
                table: "job_offers",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_job_offers_company_name",
                table: "job_offers");

            migrationBuilder.DropIndex(
                name: "IX_job_offers_published_at_utc",
                table: "job_offers");

            migrationBuilder.DropIndex(
                name: "IX_job_offers_status",
                table: "job_offers");

            migrationBuilder.RenameColumn(
                name: "title",
                table: "job_offers",
                newName: "Title");

            migrationBuilder.RenameColumn(
                name: "status",
                table: "job_offers",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "source",
                table: "job_offers",
                newName: "Source");

            migrationBuilder.RenameColumn(
                name: "location",
                table: "job_offers",
                newName: "Location");

            migrationBuilder.RenameColumn(
                name: "description",
                table: "job_offers",
                newName: "Description");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "job_offers",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "work_mode",
                table: "job_offers",
                newName: "WorkMode");

            migrationBuilder.RenameColumn(
                name: "updated_at_utc",
                table: "job_offers",
                newName: "UpdatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "source_url",
                table: "job_offers",
                newName: "SourceUrl");

            migrationBuilder.RenameColumn(
                name: "salary_min",
                table: "job_offers",
                newName: "SalaryMin");

            migrationBuilder.RenameColumn(
                name: "salary_max",
                table: "job_offers",
                newName: "SalaryMax");

            migrationBuilder.RenameColumn(
                name: "salary_currency",
                table: "job_offers",
                newName: "SalaryCurrency");

            migrationBuilder.RenameColumn(
                name: "published_at_utc",
                table: "job_offers",
                newName: "PublishedAtUtc");

            migrationBuilder.RenameColumn(
                name: "created_at_utc",
                table: "job_offers",
                newName: "CreatedAtUtc");

            migrationBuilder.RenameColumn(
                name: "contract_type",
                table: "job_offers",
                newName: "ContractType");

            migrationBuilder.RenameColumn(
                name: "company_name",
                table: "job_offers",
                newName: "CompanyName");

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                table: "job_offers",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<int>(
                name: "WorkMode",
                table: "job_offers",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<int>(
                name: "ContractType",
                table: "job_offers",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.CreateIndex(
                name: "IX_job_offers_CreatedAtUtc_Id",
                table: "job_offers",
                columns: new[] { "CreatedAtUtc", "Id" });
        }
    }
}
