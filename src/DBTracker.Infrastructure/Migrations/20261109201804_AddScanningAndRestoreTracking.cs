using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DBTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddScanningAndRestoreTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BackupFileLocation",
                table: "DatabaseInstances",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LastScanError",
                table: "DatabaseInstances",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "LastScanStatus",
                table: "DatabaseInstances",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastScanUtc",
                table: "DatabaseInstances",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetentionPolicyDays",
                table: "DatabaseInstances",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RunbookUrl",
                table: "DatabaseInstances",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "DatabaseInstances",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Source",
                table: "BackupRecords",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SourceFile",
                table: "BackupRecords",
                type: "nvarchar(260)",
                maxLength: 260,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "RestoreEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DatabaseInstanceId = table.Column<int>(type: "int", nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PerformedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<int>(type: "int", nullable: false),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestoreEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RestoreEvents_DatabaseInstances_DatabaseInstanceId",
                        column: x => x.DatabaseInstanceId,
                        principalTable: "DatabaseInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RestoreEvents_DatabaseInstanceId_TimestampUtc",
                table: "RestoreEvents",
                columns: new[] { "DatabaseInstanceId", "TimestampUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RestoreEvents");

            migrationBuilder.DropColumn(
                name: "BackupFileLocation",
                table: "DatabaseInstances");

            migrationBuilder.DropColumn(
                name: "LastScanError",
                table: "DatabaseInstances");

            migrationBuilder.DropColumn(
                name: "LastScanStatus",
                table: "DatabaseInstances");

            migrationBuilder.DropColumn(
                name: "LastScanUtc",
                table: "DatabaseInstances");

            migrationBuilder.DropColumn(
                name: "RetentionPolicyDays",
                table: "DatabaseInstances");

            migrationBuilder.DropColumn(
                name: "RunbookUrl",
                table: "DatabaseInstances");

            migrationBuilder.DropColumn(
                name: "Tags",
                table: "DatabaseInstances");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "BackupRecords");

            migrationBuilder.DropColumn(
                name: "SourceFile",
                table: "BackupRecords");
        }
    }
}
