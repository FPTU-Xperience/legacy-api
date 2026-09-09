using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ReportService.Migrations
{
    /// <inheritdoc />
    public partial class EnhanceClubActivityReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Achievements",
                table: "Reports",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Challenges",
                table: "Reports",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExecutiveSummary",
                table: "Reports",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NextPeriodPlan",
                table: "Reports",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Recommendations",
                table: "Reports",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActivityType",
                table: "ReportDetails",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BudgetSpent",
                table: "ReportDetails",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvidenceUrl",
                table: "ReportDetails",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "ReportDetails",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Objective",
                table: "ReportDetails",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PartnerUnit",
                table: "ReportDetails",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "ReportDetails",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TargetParticipantCount",
                table: "ReportDetails",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Achievements",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "Challenges",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "ExecutiveSummary",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "NextPeriodPlan",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "Recommendations",
                table: "Reports");

            migrationBuilder.DropColumn(
                name: "ActivityType",
                table: "ReportDetails");

            migrationBuilder.DropColumn(
                name: "BudgetSpent",
                table: "ReportDetails");

            migrationBuilder.DropColumn(
                name: "EvidenceUrl",
                table: "ReportDetails");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "ReportDetails");

            migrationBuilder.DropColumn(
                name: "Objective",
                table: "ReportDetails");

            migrationBuilder.DropColumn(
                name: "PartnerUnit",
                table: "ReportDetails");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "ReportDetails");

            migrationBuilder.DropColumn(
                name: "TargetParticipantCount",
                table: "ReportDetails");
        }
    }
}
