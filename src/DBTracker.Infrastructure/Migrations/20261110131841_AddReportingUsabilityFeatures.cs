using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DBTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReportingUsabilityFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "DurationMinutes",
                table: "RestoreEvents",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LinkedBackupRecordId",
                table: "RestoreEvents",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecordedByUserName",
                table: "RestoreEvents",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<double>(
                name: "TargetRecoveryTimeMinutes",
                table: "RestoreEvents",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RestoreTestIntervalDaysOverride",
                table: "DatabaseInstances",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecordedByUserName",
                table: "BackupRecords",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "BackupRecordCorrections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BackupRecordId = table.Column<int>(type: "int", nullable: false),
                    CorrectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CorrectedByUserName = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    PreviousStatus = table.Column<int>(type: "int", nullable: true),
                    NewStatus = table.Column<int>(type: "int", nullable: true),
                    PreviousNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    NewNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupRecordCorrections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BackupRecordCorrections_BackupRecords_BackupRecordId",
                        column: x => x.BackupRecordId,
                        principalTable: "BackupRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ComplianceSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GeneratedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EffectiveAsOfUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GeneratedByUserName = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    ScopeDescription = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PolicyVersion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EvidenceProvenance = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RowsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RowCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplianceSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RestoreEventCorrections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RestoreEventId = table.Column<int>(type: "int", nullable: false),
                    CorrectedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CorrectedByUserName = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    PreviousOutcome = table.Column<int>(type: "int", nullable: true),
                    NewOutcome = table.Column<int>(type: "int", nullable: true),
                    PreviousNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    NewNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestoreEventCorrections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RestoreEventCorrections_RestoreEvents_RestoreEventId",
                        column: x => x.RestoreEventId,
                        principalTable: "RestoreEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RestoreEvents_LinkedBackupRecordId",
                table: "RestoreEvents",
                column: "LinkedBackupRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_BackupRecordCorrections_BackupRecordId",
                table: "BackupRecordCorrections",
                column: "BackupRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceSnapshots_GeneratedAtUtc",
                table: "ComplianceSnapshots",
                column: "GeneratedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_RestoreEventCorrections_RestoreEventId",
                table: "RestoreEventCorrections",
                column: "RestoreEventId");

            migrationBuilder.AddForeignKey(
                name: "FK_RestoreEvents_BackupRecords_LinkedBackupRecordId",
                table: "RestoreEvents",
                column: "LinkedBackupRecordId",
                principalTable: "BackupRecords",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RestoreEvents_BackupRecords_LinkedBackupRecordId",
                table: "RestoreEvents");

            migrationBuilder.DropTable(
                name: "BackupRecordCorrections");

            migrationBuilder.DropTable(
                name: "ComplianceSnapshots");

            migrationBuilder.DropTable(
                name: "RestoreEventCorrections");

            migrationBuilder.DropIndex(
                name: "IX_RestoreEvents_LinkedBackupRecordId",
                table: "RestoreEvents");

            migrationBuilder.DropColumn(
                name: "DurationMinutes",
                table: "RestoreEvents");

            migrationBuilder.DropColumn(
                name: "LinkedBackupRecordId",
                table: "RestoreEvents");

            migrationBuilder.DropColumn(
                name: "RecordedByUserName",
                table: "RestoreEvents");

            migrationBuilder.DropColumn(
                name: "TargetRecoveryTimeMinutes",
                table: "RestoreEvents");

            migrationBuilder.DropColumn(
                name: "RestoreTestIntervalDaysOverride",
                table: "DatabaseInstances");

            migrationBuilder.DropColumn(
                name: "RecordedByUserName",
                table: "BackupRecords");
        }
    }
}
