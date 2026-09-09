using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClubService.Migrations
{
    /// <inheritdoc />
    public partial class AddClubMemberActivityManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAtUtc",
                table: "ClubMemberships",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeletedByUserId",
                table: "ClubMemberships",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ClubMemberships",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_ClubMemberships_ClubId_IsDeleted_Status",
                table: "ClubMemberships",
                columns: new[] { "ClubId", "IsDeleted", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ClubMemberships_ClubId_ReviewedAtUtc",
                table: "ClubMemberships",
                columns: new[] { "ClubId", "ReviewedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ClubMemberships_ClubId_IsDeleted_Status",
                table: "ClubMemberships");

            migrationBuilder.DropIndex(
                name: "IX_ClubMemberships_ClubId_ReviewedAtUtc",
                table: "ClubMemberships");

            migrationBuilder.DropColumn(
                name: "DeletedAtUtc",
                table: "ClubMemberships");

            migrationBuilder.DropColumn(
                name: "DeletedByUserId",
                table: "ClubMemberships");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ClubMemberships");
        }
    }
}
