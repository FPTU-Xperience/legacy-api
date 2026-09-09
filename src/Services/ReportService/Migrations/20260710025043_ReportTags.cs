using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReportService.Migrations
{
    /// <inheritdoc />
    public partial class ReportTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reports_ClubId_Period",
                table: "Reports");

            migrationBuilder.AddColumn<string>(
                name: "ReportType",
                table: "Reports",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "Activity report");

            migrationBuilder.AddColumn<string>(
                name: "Tag",
                table: "Reports",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "Activity report");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_ClubId_Period_Tag",
                table: "Reports",
                columns: new[] { "ClubId", "Period", "Tag" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Reports_ClubId_Period_Tag",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "ReportType",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "Tag",
                table: "Reports");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_ClubId_Period",
                table: "Reports",
                columns: new[] { "ClubId", "Period" },
                unique: true);
        }
    }
}
