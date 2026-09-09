using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExportService.Migrations
{
    /// <inheritdoc />
    public partial class AddReportSnapshotFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ReportId",
                table: "ExportRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SnapshotJson",
                table: "ExportRequests",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReportId",
                table: "ExportRequests");

            migrationBuilder.DropColumn(
                name: "SnapshotJson",
                table: "ExportRequests");
        }
    }
}
