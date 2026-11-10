using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DBTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUsabilityEpics14And15 : Migration
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

            migrationBuilder.AddColumn<string>(
                name: "Company",
                table: "Notifications",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

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
                name: "Incidents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DatabaseInstanceId = table.Column<int>(type: "int", nullable: false),
                    TriggerHealth = table.Column<int>(type: "int", nullable: false),
                    OpenedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AssigneeName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AcknowledgedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AcknowledgedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    TicketReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EscalationLevel = table.Column<int>(type: "int", nullable: false),
                    LastEscalatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Incidents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Incidents_DatabaseInstances_DatabaseInstanceId",
                        column: x => x.DatabaseInstanceId,
                        principalTable: "DatabaseInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NotificationReadStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    NotificationId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ReadAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationReadStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationReadStates_Notifications_NotificationId",
                        column: x => x.NotificationId,
                        principalTable: "Notifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NotificationSubscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    DatabaseInstanceId = table.Column<int>(type: "int", nullable: true),
                    Company = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Channel = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotificationSubscriptions_DatabaseInstances_DatabaseInstanceId",
                        column: x => x.DatabaseInstanceId,
                        principalTable: "DatabaseInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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

            migrationBuilder.CreateTable(
                name: "SavedViews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ViewType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    QueryString = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedViews", x => x.Id);
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
                name: "IX_Incidents_DatabaseInstanceId_ClosedAtUtc",
                table: "Incidents",
                columns: new[] { "DatabaseInstanceId", "ClosedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationReadStates_NotificationId_UserId",
                table: "NotificationReadStates",
                columns: new[] { "NotificationId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationReadStates_UserId",
                table: "NotificationReadStates",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationSubscriptions_DatabaseInstanceId",
                table: "NotificationSubscriptions",
                column: "DatabaseInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationSubscriptions_UserId",
                table: "NotificationSubscriptions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_RestoreEventCorrections_RestoreEventId",
                table: "RestoreEventCorrections",
                column: "RestoreEventId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedViews_UserId_ViewType",
                table: "SavedViews",
                columns: new[] { "UserId", "ViewType" });

            migrationBuilder.CreateIndex(
                name: "IX_SavedViews_UserId_ViewType_Name",
                table: "SavedViews",
                columns: new[] { "UserId", "ViewType", "Name" },
                unique: true);

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
                name: "Incidents");

            migrationBuilder.DropTable(
                name: "NotificationReadStates");

            migrationBuilder.DropTable(
                name: "NotificationSubscriptions");

            migrationBuilder.DropTable(
                name: "RestoreEventCorrections");

            migrationBuilder.DropTable(
                name: "SavedViews");

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
                name: "Company",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "RestoreTestIntervalDaysOverride",
                table: "DatabaseInstances");

            migrationBuilder.DropColumn(
                name: "RecordedByUserName",
                table: "BackupRecords");
        }
    }
}
