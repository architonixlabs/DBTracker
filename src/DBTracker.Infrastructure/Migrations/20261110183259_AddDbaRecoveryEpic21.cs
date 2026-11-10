using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DBTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDbaRecoveryEpic21 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ChainRole",
                table: "BackupRecords",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BackupChainRequirements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DatabaseInstanceId = table.Column<int>(type: "int", nullable: false),
                    MemberType = table.Column<int>(type: "int", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    MaxAgeHoursOverride = table.Column<int>(type: "int", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupChainRequirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BackupChainRequirements_DatabaseInstances_DatabaseInstanceId",
                        column: x => x.DatabaseInstanceId,
                        principalTable: "DatabaseInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BackupCopyLocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DatabaseInstanceId = table.Column<int>(type: "int", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IsOffsite = table.Column<bool>(type: "bit", nullable: false),
                    IsImmutableFlagged = table.Column<bool>(type: "bit", nullable: false),
                    EncryptionExpected = table.Column<bool>(type: "bit", nullable: false),
                    StoragePathOrReference = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    FirstDiscoveredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VerificationStatus = table.Column<int>(type: "int", nullable: false),
                    LastVerifiedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastVerifiedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    VerificationNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    LegalHoldReference = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupCopyLocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BackupCopyLocations_DatabaseInstances_DatabaseInstanceId",
                        column: x => x.DatabaseInstanceId,
                        principalTable: "DatabaseInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CapacityMeasurements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DatabaseInstanceId = table.Column<int>(type: "int", nullable: false),
                    MeasurementType = table.Column<int>(type: "int", nullable: false),
                    MeasuredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Value = table.Column<double>(type: "float", nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    MeasurementPeriodDescription = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RecordedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapacityMeasurements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CapacityMeasurements_DatabaseInstances_DatabaseInstanceId",
                        column: x => x.DatabaseInstanceId,
                        principalTable: "DatabaseInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CertificateDependencies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DatabaseInstanceId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OwnerTeamId = table.Column<int>(type: "int", nullable: true),
                    OwnerName = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SecretStoreReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    VerifiedBackupRestoreAccess = table.Column<bool>(type: "bit", nullable: false),
                    LastAccessCheckAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastAccessCheckBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    LastRotatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RotationIntervalDays = table.Column<int>(type: "int", nullable: true),
                    AffectedReplicasOrClientsText = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    LastRenewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RenewalEvidenceText = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    LastExpiryNoticeThresholdDays = table.Column<int>(type: "int", nullable: true),
                    LastExpiryNoticeAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CertificateDependencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CertificateDependencies_DatabaseInstances_DatabaseInstanceId",
                        column: x => x.DatabaseInstanceId,
                        principalTable: "DatabaseInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CertificateDependencies_Teams_OwnerTeamId",
                        column: x => x.OwnerTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceEvidenceRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DatabaseInstanceId = table.Column<int>(type: "int", nullable: false),
                    CheckType = table.Column<int>(type: "int", nullable: false),
                    NodeOrDeploymentLabel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RelatedDependencyId = table.Column<int>(type: "int", nullable: true),
                    ToolName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ToolVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ScheduleDescription = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ExpectedIntervalHours = table.Column<int>(type: "int", nullable: true),
                    RanAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Source = table.Column<int>(type: "int", nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RecordedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceEvidenceRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceEvidenceRecords_DatabaseInstances_DatabaseInstanceId",
                        column: x => x.DatabaseInstanceId,
                        principalTable: "DatabaseInstances",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MaintenanceEvidenceRecords_InstanceDependencies_RelatedDependencyId",
                        column: x => x.RelatedDependencyId,
                        principalTable: "InstanceDependencies",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MigrationCapacityEstimates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DatabaseInstanceId = table.Column<int>(type: "int", nullable: false),
                    MigrationCampaignId = table.Column<int>(type: "int", nullable: true),
                    PathAssessmentId = table.Column<int>(type: "int", nullable: true),
                    EstimatedSpaceGb = table.Column<double>(type: "float", nullable: true),
                    AssumptionsText = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    MissingDataNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    EstimatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EstimatedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MigrationCapacityEstimates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MigrationCapacityEstimates_DatabaseInstances_DatabaseInstanceId",
                        column: x => x.DatabaseInstanceId,
                        principalTable: "DatabaseInstances",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MigrationCapacityEstimates_MigrationCampaigns_MigrationCampaignId",
                        column: x => x.MigrationCampaignId,
                        principalTable: "MigrationCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MigrationCapacityEstimates_PathAssessments_PathAssessmentId",
                        column: x => x.PathAssessmentId,
                        principalTable: "PathAssessments",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "RecoveryObjectives",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DatabaseInstanceId = table.Column<int>(type: "int", nullable: false),
                    RpoTargetMinutes = table.Column<int>(type: "int", nullable: true),
                    RtoTargetMinutes = table.Column<int>(type: "int", nullable: true),
                    EncryptionKeyReferenceNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OffsiteCopyReferenceNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecoveryObjectives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecoveryObjectives_DatabaseInstances_DatabaseInstanceId",
                        column: x => x.DatabaseInstanceId,
                        principalTable: "DatabaseInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkloadComparisons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DatabaseInstanceId = table.Column<int>(type: "int", nullable: false),
                    MigrationCampaignId = table.Column<int>(type: "int", nullable: true),
                    PreMeasurementId = table.Column<int>(type: "int", nullable: true),
                    PostMeasurementId = table.Column<int>(type: "int", nullable: true),
                    ComparisonNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RecordedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkloadComparisons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkloadComparisons_CapacityMeasurements_PostMeasurementId",
                        column: x => x.PostMeasurementId,
                        principalTable: "CapacityMeasurements",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkloadComparisons_CapacityMeasurements_PreMeasurementId",
                        column: x => x.PreMeasurementId,
                        principalTable: "CapacityMeasurements",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkloadComparisons_DatabaseInstances_DatabaseInstanceId",
                        column: x => x.DatabaseInstanceId,
                        principalTable: "DatabaseInstances",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkloadComparisons_MigrationCampaigns_MigrationCampaignId",
                        column: x => x.MigrationCampaignId,
                        principalTable: "MigrationCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BackupChainRequirements_DatabaseInstanceId_MemberType",
                table: "BackupChainRequirements",
                columns: new[] { "DatabaseInstanceId", "MemberType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BackupCopyLocations_DatabaseInstanceId_VerificationStatus",
                table: "BackupCopyLocations",
                columns: new[] { "DatabaseInstanceId", "VerificationStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_CapacityMeasurements_DatabaseInstanceId_MeasurementType_MeasuredAtUtc",
                table: "CapacityMeasurements",
                columns: new[] { "DatabaseInstanceId", "MeasurementType", "MeasuredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CertificateDependencies_DatabaseInstanceId_Kind",
                table: "CertificateDependencies",
                columns: new[] { "DatabaseInstanceId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_CertificateDependencies_ExpiresAtUtc",
                table: "CertificateDependencies",
                column: "ExpiresAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CertificateDependencies_OwnerTeamId",
                table: "CertificateDependencies",
                column: "OwnerTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceEvidenceRecords_DatabaseInstanceId_CheckType_RanAtUtc",
                table: "MaintenanceEvidenceRecords",
                columns: new[] { "DatabaseInstanceId", "CheckType", "RanAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceEvidenceRecords_RelatedDependencyId",
                table: "MaintenanceEvidenceRecords",
                column: "RelatedDependencyId");

            migrationBuilder.CreateIndex(
                name: "IX_MigrationCapacityEstimates_DatabaseInstanceId",
                table: "MigrationCapacityEstimates",
                column: "DatabaseInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_MigrationCapacityEstimates_MigrationCampaignId",
                table: "MigrationCapacityEstimates",
                column: "MigrationCampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_MigrationCapacityEstimates_PathAssessmentId",
                table: "MigrationCapacityEstimates",
                column: "PathAssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_RecoveryObjectives_DatabaseInstanceId",
                table: "RecoveryObjectives",
                column: "DatabaseInstanceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkloadComparisons_DatabaseInstanceId",
                table: "WorkloadComparisons",
                column: "DatabaseInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkloadComparisons_MigrationCampaignId",
                table: "WorkloadComparisons",
                column: "MigrationCampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkloadComparisons_PostMeasurementId",
                table: "WorkloadComparisons",
                column: "PostMeasurementId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkloadComparisons_PreMeasurementId",
                table: "WorkloadComparisons",
                column: "PreMeasurementId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BackupChainRequirements");

            migrationBuilder.DropTable(
                name: "BackupCopyLocations");

            migrationBuilder.DropTable(
                name: "CertificateDependencies");

            migrationBuilder.DropTable(
                name: "MaintenanceEvidenceRecords");

            migrationBuilder.DropTable(
                name: "MigrationCapacityEstimates");

            migrationBuilder.DropTable(
                name: "RecoveryObjectives");

            migrationBuilder.DropTable(
                name: "WorkloadComparisons");

            migrationBuilder.DropTable(
                name: "CapacityMeasurements");

            migrationBuilder.DropColumn(
                name: "ChainRole",
                table: "BackupRecords");
        }
    }
}
