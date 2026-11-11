using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DBTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddImportedEvidenceAndEventTimeZones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EvidenceReference",
                table: "RestoreEvents",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OccurredTimeZone",
                table: "RestoreEvents",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Source",
                table: "RestoreEvents",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "OccurredTimeZone",
                table: "BackupRecords",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EvidenceReference",
                table: "RestoreEvents");

            migrationBuilder.DropColumn(
                name: "OccurredTimeZone",
                table: "RestoreEvents");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "RestoreEvents");

            migrationBuilder.DropColumn(
                name: "OccurredTimeZone",
                table: "BackupRecords");
        }
    }
}
