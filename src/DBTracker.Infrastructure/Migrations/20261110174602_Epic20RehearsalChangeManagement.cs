using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DBTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Epic20RehearsalChangeManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Criticality",
                table: "DatabaseInstances",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ChangeValidations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MigrationCampaignId = table.Column<int>(type: "int", nullable: false),
                    DatabaseInstanceId = table.Column<int>(type: "int", nullable: false),
                    ObservedTargetBuildText = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ObservedCompatibilityLevel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ApplicationTestsOutcome = table.Column<int>(type: "int", nullable: false),
                    ApplicationTestsNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    IntegrityCheckOutcome = table.Column<int>(type: "int", nullable: false),
                    IntegrityCheckNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    PerformanceComparisonNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    JobsPermissionsOutcome = table.Column<int>(type: "int", nullable: false),
                    HaReplicationStateOutcome = table.Column<int>(type: "int", nullable: false),
                    NewBackupRestoreEvidenceOutcome = table.Column<int>(type: "int", nullable: false),
                    LinkedBackupRecordId = table.Column<int>(type: "int", nullable: true),
                    LinkedRestoreEventId = table.Column<int>(type: "int", nullable: true),
                    BeforeEvidenceJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AfterEvidenceJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValidationTimeUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RollbackRetentionDeadlineUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RecordedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeValidations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChangeValidations_BackupRecords_LinkedBackupRecordId",
                        column: x => x.LinkedBackupRecordId,
                        principalTable: "BackupRecords",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ChangeValidations_DatabaseInstances_DatabaseInstanceId",
                        column: x => x.DatabaseInstanceId,
                        principalTable: "DatabaseInstances",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ChangeValidations_MigrationCampaigns_MigrationCampaignId",
                        column: x => x.MigrationCampaignId,
                        principalTable: "MigrationCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChangeValidations_RestoreEvents_LinkedRestoreEventId",
                        column: x => x.LinkedRestoreEventId,
                        principalTable: "RestoreEvents",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ReadinessExceptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TargetType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TargetId = table.Column<int>(type: "int", nullable: false),
                    WaivedCheckReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CompensatingControls = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    AccountableOwnerTeamId = table.Column<int>(type: "int", nullable: true),
                    AccountableOwnerName = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    PreparedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    PreparedByUserName = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    RequiresSeparateApprover = table.Column<bool>(type: "bit", nullable: false),
                    PermittedApproverRole = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ApprovedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ApprovedByUserName = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecisionNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    EvidenceUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    EvidenceNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    IsVendorSupportCoverage = table.Column<bool>(type: "bit", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    RevokedReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RenewalOfExceptionId = table.Column<int>(type: "int", nullable: true),
                    ExpiryNotifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadinessExceptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReadinessExceptions_ReadinessExceptions_RenewalOfExceptionId",
                        column: x => x.RenewalOfExceptionId,
                        principalTable: "ReadinessExceptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReadinessExceptions_Teams_AccountableOwnerTeamId",
                        column: x => x.AccountableOwnerTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "RehearsalRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MigrationCampaignId = table.Column<int>(type: "int", nullable: false),
                    DatabaseInstanceId = table.Column<int>(type: "int", nullable: false),
                    PathAssessmentId = table.Column<int>(type: "int", nullable: true),
                    SourceLabel = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    TargetLabel = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    RepresentativeDatasetDescription = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false),
                    PerformedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PerformedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DurationMinutes = table.Column<double>(type: "float", nullable: true),
                    BackupRestoreOutcome = table.Column<int>(type: "int", nullable: false),
                    LinkedRestoreEventId = table.Column<int>(type: "int", nullable: true),
                    CorrectnessCheckOutcome = table.Column<int>(type: "int", nullable: false),
                    CorrectnessCheckNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    PerformanceCheckOutcome = table.Column<int>(type: "int", nullable: false),
                    PerformanceComparisonNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    TargetRpoMinutes = table.Column<double>(type: "float", nullable: true),
                    TargetRtoMinutes = table.Column<double>(type: "float", nullable: true),
                    MeasuredRpoMinutes = table.Column<double>(type: "float", nullable: true),
                    MeasuredRtoMinutes = table.Column<double>(type: "float", nullable: true),
                    PrerequisitesText = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    RecoveryKeyDependenciesText = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    PostCutoverWriteDisposition = table.Column<int>(type: "int", nullable: false),
                    PostCutoverWriteReconciliationPlan = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ValidityDays = table.Column<int>(type: "int", nullable: true),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RecordedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RehearsalRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RehearsalRecords_DatabaseInstances_DatabaseInstanceId",
                        column: x => x.DatabaseInstanceId,
                        principalTable: "DatabaseInstances",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RehearsalRecords_MigrationCampaigns_MigrationCampaignId",
                        column: x => x.MigrationCampaignId,
                        principalTable: "MigrationCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RehearsalRecords_PathAssessments_PathAssessmentId",
                        column: x => x.PathAssessmentId,
                        principalTable: "PathAssessments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RehearsalRecords_RestoreEvents_LinkedRestoreEventId",
                        column: x => x.LinkedRestoreEventId,
                        principalTable: "RestoreEvents",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ChangeValidationSignoffs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChangeValidationId = table.Column<int>(type: "int", nullable: false),
                    ReviewerScope = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReviewerName = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    SignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeValidationSignoffs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChangeValidationSignoffs_ChangeValidations_ChangeValidationId",
                        column: x => x.ChangeValidationId,
                        principalTable: "ChangeValidations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChangeWindows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MigrationCampaignId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    PlannedStartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PlannedEndUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TimeZoneLabel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OutageEstimateMinutes = table.Column<int>(type: "int", nullable: true),
                    CommunicationOwnerTeamId = table.Column<int>(type: "int", nullable: true),
                    RunbookNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    EmergencyExceptionId = table.Column<int>(type: "int", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeWindows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChangeWindows_MigrationCampaigns_MigrationCampaignId",
                        column: x => x.MigrationCampaignId,
                        principalTable: "MigrationCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ChangeWindows_ReadinessExceptions_EmergencyExceptionId",
                        column: x => x.EmergencyExceptionId,
                        principalTable: "ReadinessExceptions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ChangeWindows_Teams_CommunicationOwnerTeamId",
                        column: x => x.CommunicationOwnerTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "RehearsalRollbackDecisionPoints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RehearsalRecordId = table.Column<int>(type: "int", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    TriggerCondition = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IsPointOfNoReturn = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RehearsalRollbackDecisionPoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RehearsalRollbackDecisionPoints_RehearsalRecords_RehearsalRecordId",
                        column: x => x.RehearsalRecordId,
                        principalTable: "RehearsalRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChangeWindowApprovals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChangeWindowId = table.Column<int>(type: "int", nullable: false),
                    RequiredScope = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Decision = table.Column<int>(type: "int", nullable: true),
                    ApproverName = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    ApproverUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeWindowApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChangeWindowApprovals_ChangeWindows_ChangeWindowId",
                        column: x => x.ChangeWindowId,
                        principalTable: "ChangeWindows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChangeWindowRunbookSteps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChangeWindowId = table.Column<int>(type: "int", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    IsCompleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeWindowRunbookSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChangeWindowRunbookSteps_ChangeWindows_ChangeWindowId",
                        column: x => x.ChangeWindowId,
                        principalTable: "ChangeWindows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CutoverJournalEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChangeWindowId = table.Column<int>(type: "int", nullable: false),
                    EntryType = table.Column<int>(type: "int", nullable: false),
                    TimestampUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    TrafficOrWriteStateNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Decision = table.Column<int>(type: "int", nullable: false),
                    RecordedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CutoverJournalEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CutoverJournalEntries_ChangeWindows_ChangeWindowId",
                        column: x => x.ChangeWindowId,
                        principalTable: "ChangeWindows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DatabaseInstances_Criticality",
                table: "DatabaseInstances",
                column: "Criticality");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeValidations_DatabaseInstanceId_RecordedAtUtc",
                table: "ChangeValidations",
                columns: new[] { "DatabaseInstanceId", "RecordedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ChangeValidations_LinkedBackupRecordId",
                table: "ChangeValidations",
                column: "LinkedBackupRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeValidations_LinkedRestoreEventId",
                table: "ChangeValidations",
                column: "LinkedRestoreEventId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeValidations_MigrationCampaignId",
                table: "ChangeValidations",
                column: "MigrationCampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeValidationSignoffs_ChangeValidationId",
                table: "ChangeValidationSignoffs",
                column: "ChangeValidationId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeWindowApprovals_ChangeWindowId",
                table: "ChangeWindowApprovals",
                column: "ChangeWindowId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeWindowRunbookSteps_ChangeWindowId",
                table: "ChangeWindowRunbookSteps",
                column: "ChangeWindowId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeWindows_CommunicationOwnerTeamId",
                table: "ChangeWindows",
                column: "CommunicationOwnerTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeWindows_EmergencyExceptionId",
                table: "ChangeWindows",
                column: "EmergencyExceptionId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeWindows_MigrationCampaignId",
                table: "ChangeWindows",
                column: "MigrationCampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeWindows_PlannedStartUtc",
                table: "ChangeWindows",
                column: "PlannedStartUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CutoverJournalEntries_ChangeWindowId_TimestampUtc",
                table: "CutoverJournalEntries",
                columns: new[] { "ChangeWindowId", "TimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ReadinessExceptions_AccountableOwnerTeamId",
                table: "ReadinessExceptions",
                column: "AccountableOwnerTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadinessExceptions_ExpiresAtUtc",
                table: "ReadinessExceptions",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ReadinessExceptions_RenewalOfExceptionId",
                table: "ReadinessExceptions",
                column: "RenewalOfExceptionId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadinessExceptions_Status",
                table: "ReadinessExceptions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ReadinessExceptions_TargetType_TargetId",
                table: "ReadinessExceptions",
                columns: new[] { "TargetType", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "IX_RehearsalRecords_DatabaseInstanceId_PerformedAtUtc",
                table: "RehearsalRecords",
                columns: new[] { "DatabaseInstanceId", "PerformedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_RehearsalRecords_LinkedRestoreEventId",
                table: "RehearsalRecords",
                column: "LinkedRestoreEventId");

            migrationBuilder.CreateIndex(
                name: "IX_RehearsalRecords_MigrationCampaignId",
                table: "RehearsalRecords",
                column: "MigrationCampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_RehearsalRecords_PathAssessmentId",
                table: "RehearsalRecords",
                column: "PathAssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_RehearsalRollbackDecisionPoints_RehearsalRecordId",
                table: "RehearsalRollbackDecisionPoints",
                column: "RehearsalRecordId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChangeValidationSignoffs");

            migrationBuilder.DropTable(
                name: "ChangeWindowApprovals");

            migrationBuilder.DropTable(
                name: "ChangeWindowRunbookSteps");

            migrationBuilder.DropTable(
                name: "CutoverJournalEntries");

            migrationBuilder.DropTable(
                name: "RehearsalRollbackDecisionPoints");

            migrationBuilder.DropTable(
                name: "ChangeValidations");

            migrationBuilder.DropTable(
                name: "ChangeWindows");

            migrationBuilder.DropTable(
                name: "RehearsalRecords");

            migrationBuilder.DropTable(
                name: "ReadinessExceptions");

            migrationBuilder.DropIndex(
                name: "IX_DatabaseInstances_Criticality",
                table: "DatabaseInstances");

            migrationBuilder.DropColumn(
                name: "Criticality",
                table: "DatabaseInstances");
        }
    }
}
