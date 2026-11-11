using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DBTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLifecycleNoticeMilestonesAndCatalogueFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ObservedCompatibilityLevel",
                table: "VersionObservations",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ObservedEdition",
                table: "VersionObservations",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LastRunwayNoticeKind",
                table: "VersionIdentities",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastRunwayNoticeMilestoneUtc",
                table: "VersionIdentities",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReleaseNotesUrl",
                table: "ReleaseCatalogEntries",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "RequiredByUtc",
                table: "ApprovedPatchBaselines",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ObservedCompatibilityLevel",
                table: "VersionObservations");

            migrationBuilder.DropColumn(
                name: "ObservedEdition",
                table: "VersionObservations");

            migrationBuilder.DropColumn(
                name: "LastRunwayNoticeKind",
                table: "VersionIdentities");

            migrationBuilder.DropColumn(
                name: "LastRunwayNoticeMilestoneUtc",
                table: "VersionIdentities");

            migrationBuilder.DropColumn(
                name: "ReleaseNotesUrl",
                table: "ReleaseCatalogEntries");

            migrationBuilder.DropColumn(
                name: "RequiredByUtc",
                table: "ApprovedPatchBaselines");
        }
    }
}
