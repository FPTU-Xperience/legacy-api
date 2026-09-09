using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ClubService.Migrations
{
    /// <inheritdoc />
    public partial class ExpandedClubApplicationForms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "Clubs",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "OTHER");

            migrationBuilder.AddColumn<string>(
                name: "LogoUrl",
                table: "Clubs",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AcceptedClubRules",
                table: "ClubMemberships",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "AdditionalInfoJson",
                table: "ClubMemberships",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "ClubMemberships",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "CommittedToParticipate",
                table: "ClubMemberships",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Contributions",
                table: "ClubMemberships",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateOnly>(
                name: "DateOfBirth",
                table: "ClubMemberships",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "ClubMemberships",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Expectations",
                table: "ClubMemberships",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Gender",
                table: "ClubMemberships",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Hobbies",
                table: "ClubMemberships",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PhoneNumber",
                table: "ClubMemberships",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Skills",
                table: "ClubMemberships",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ActivityFrequency",
                table: "ClubCreationApplications",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "AdvisorNeeded",
                table: "ClubCreationApplications",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                table: "ClubCreationApplications",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "OTHER");

            migrationBuilder.AddColumn<bool>(
                name: "CommittedToReporting",
                table: "ClubCreationApplications",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CommittedToResponsibility",
                table: "ClubCreationApplications",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CommittedToRules",
                table: "ClubCreationApplications",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "EquipmentNeeds",
                table: "ClubCreationApplications",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ExpectedLocation",
                table: "ClubCreationApplications",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ExpectedSchedule",
                table: "ClubCreationApplications",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FounderOrganization",
                table: "ClubCreationApplications",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FounderRole",
                table: "ClubCreationApplications",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "FoundingMemberCount",
                table: "ClubCreationApplications",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "FoundingMembersCommitted",
                table: "ClubCreationApplications",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "FoundingMembersJson",
                table: "ClubCreationApplications",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "FundingSupport",
                table: "ClubCreationApplications",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "SELF_FUNDED");

            migrationBuilder.AddColumn<string>(
                name: "LogoUrl",
                table: "ClubCreationApplications",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MainActivities",
                table: "ClubCreationApplications",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MajorEvents",
                table: "ClubCreationApplications",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReviewConditions",
                table: "ClubCreationApplications",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewerSignature",
                table: "ClubCreationApplications",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VenueSupport",
                table: "ClubCreationApplications",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "SELF_MANAGED");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Category",
                table: "Clubs");

            migrationBuilder.DropColumn(
                name: "LogoUrl",
                table: "Clubs");

            migrationBuilder.DropColumn(
                name: "AcceptedClubRules",
                table: "ClubMemberships");

            migrationBuilder.DropColumn(
                name: "AdditionalInfoJson",
                table: "ClubMemberships");

            migrationBuilder.DropColumn(
                name: "Address",
                table: "ClubMemberships");

            migrationBuilder.DropColumn(
                name: "CommittedToParticipate",
                table: "ClubMemberships");

            migrationBuilder.DropColumn(
                name: "Contributions",
                table: "ClubMemberships");

            migrationBuilder.DropColumn(
                name: "DateOfBirth",
                table: "ClubMemberships");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "ClubMemberships");

            migrationBuilder.DropColumn(
                name: "Expectations",
                table: "ClubMemberships");

            migrationBuilder.DropColumn(
                name: "Gender",
                table: "ClubMemberships");

            migrationBuilder.DropColumn(
                name: "Hobbies",
                table: "ClubMemberships");

            migrationBuilder.DropColumn(
                name: "PhoneNumber",
                table: "ClubMemberships");

            migrationBuilder.DropColumn(
                name: "Skills",
                table: "ClubMemberships");

            migrationBuilder.DropColumn(
                name: "ActivityFrequency",
                table: "ClubCreationApplications");

            migrationBuilder.DropColumn(
                name: "AdvisorNeeded",
                table: "ClubCreationApplications");

            migrationBuilder.DropColumn(
                name: "Category",
                table: "ClubCreationApplications");

            migrationBuilder.DropColumn(
                name: "CommittedToReporting",
                table: "ClubCreationApplications");

            migrationBuilder.DropColumn(
                name: "CommittedToResponsibility",
                table: "ClubCreationApplications");

            migrationBuilder.DropColumn(
                name: "CommittedToRules",
                table: "ClubCreationApplications");

            migrationBuilder.DropColumn(
                name: "EquipmentNeeds",
                table: "ClubCreationApplications");

            migrationBuilder.DropColumn(
                name: "ExpectedLocation",
                table: "ClubCreationApplications");

            migrationBuilder.DropColumn(
                name: "ExpectedSchedule",
                table: "ClubCreationApplications");

            migrationBuilder.DropColumn(
                name: "FounderOrganization",
                table: "ClubCreationApplications");

            migrationBuilder.DropColumn(
                name: "FounderRole",
                table: "ClubCreationApplications");

            migrationBuilder.DropColumn(
                name: "FoundingMemberCount",
                table: "ClubCreationApplications");

            migrationBuilder.DropColumn(
                name: "FoundingMembersCommitted",
                table: "ClubCreationApplications");

            migrationBuilder.DropColumn(
                name: "FoundingMembersJson",
                table: "ClubCreationApplications");

            migrationBuilder.DropColumn(
                name: "FundingSupport",
                table: "ClubCreationApplications");

            migrationBuilder.DropColumn(
                name: "LogoUrl",
                table: "ClubCreationApplications");

            migrationBuilder.DropColumn(
                name: "MainActivities",
                table: "ClubCreationApplications");

            migrationBuilder.DropColumn(
                name: "MajorEvents",
                table: "ClubCreationApplications");

            migrationBuilder.DropColumn(
                name: "ReviewConditions",
                table: "ClubCreationApplications");

            migrationBuilder.DropColumn(
                name: "ReviewerSignature",
                table: "ClubCreationApplications");

            migrationBuilder.DropColumn(
                name: "VenueSupport",
                table: "ClubCreationApplications");
        }
    }
}
