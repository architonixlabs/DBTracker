using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DBTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDbaLifecycleEpic18 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdvisoryRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Engine = table.Column<int>(type: "int", nullable: false),
                    Family = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    VendorAdvisoryId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Severity = table.Column<int>(type: "int", nullable: false),
                    AffectedMinBuild = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AffectedMaxBuild = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AffectedConfiguration = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FixedBuild = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProvenanceUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ImportedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ImportedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdvisoryRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReleaseCatalogEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Engine = table.Column<int>(type: "int", nullable: false),
                    Family = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReleaseLabel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Major = table.Column<int>(type: "int", nullable: true),
                    Minor = table.Column<int>(type: "int", nullable: true),
                    Build = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Channel = table.Column<int>(type: "int", nullable: false),
                    PatchTrain = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Edition = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Distribution = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ApplicabilityNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    GaDateUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GaDatePrecision = table.Column<int>(type: "int", nullable: false),
                    MainstreamSupportEndUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MainstreamSupportEndPrecision = table.Column<int>(type: "int", nullable: false),
                    ExtendedSupportEndUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExtendedSupportEndPrecision = table.Column<int>(type: "int", nullable: false),
                    EsuSupportEndUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EsuSupportEndPrecision = table.Column<int>(type: "int", nullable: false),
                    DatesAreTbd = table.Column<bool>(type: "bit", nullable: false),
                    DatesConflict = table.Column<bool>(type: "bit", nullable: false),
                    DateTimezone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SourceUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SourceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RetrievedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    IsManualOverride = table.Column<bool>(type: "bit", nullable: false),
                    OverrideReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    OverrideReviewedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    OverrideExpiresUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsStale = table.Column<bool>(type: "bit", nullable: false),
                    LastRefreshAttemptUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastRefreshError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RevisionNumber = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseCatalogEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VersionIdentities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DatabaseInstanceId = table.Column<int>(type: "int", nullable: false),
                    RawVersionText = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ResolutionStatus = table.Column<int>(type: "int", nullable: false),
                    UnresolvedReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Family = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Major = table.Column<int>(type: "int", nullable: true),
                    Minor = table.Column<int>(type: "int", nullable: true),
                    Build = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Edition = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Distribution = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PatchTrain = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    HostingProvider = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    HostingContext = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    HostingEngineVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DatabaseCompatibilityLevel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ObservationSource = table.Column<int>(type: "int", nullable: false),
                    ObservedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsAttested = table.Column<bool>(type: "bit", nullable: false),
                    AttestedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    AttestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastRunwayNoticeThresholdDays = table.Column<int>(type: "int", nullable: true),
                    LastRunwayNoticeAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VersionIdentities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VersionIdentities_DatabaseInstances_DatabaseInstanceId",
                        column: x => x.DatabaseInstanceId,
                        principalTable: "DatabaseInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VersionObservations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DatabaseInstanceId = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<int>(type: "int", nullable: false),
                    RawVersionText = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Outcome = table.Column<int>(type: "int", nullable: false),
                    FailureDetail = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ObservedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ObservedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    DriftStatus = table.Column<int>(type: "int", nullable: false),
                    DriftDetail = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VersionObservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VersionObservations_DatabaseInstances_DatabaseInstanceId",
                        column: x => x.DatabaseInstanceId,
                        principalTable: "DatabaseInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AdvisoryInstanceAssessments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AdvisoryRecordId = table.Column<int>(type: "int", nullable: false),
                    DatabaseInstanceId = table.Column<int>(type: "int", nullable: false),
                    Classification = table.Column<int>(type: "int", nullable: false),
                    ClassificationReasoning = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DistributionProvidesBackport = table.Column<bool>(type: "bit", nullable: false),
                    ManagedServiceResponsibility = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsReviewed = table.Column<bool>(type: "bit", nullable: false),
                    ReviewerEvidence = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ReviewedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Owner = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    RemediationDeadlineUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TargetPatch = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    VerificationStatus = table.Column<int>(type: "int", nullable: false),
                    VerificationNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdvisoryInstanceAssessments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdvisoryInstanceAssessments_AdvisoryRecords_AdvisoryRecordId",
                        column: x => x.AdvisoryRecordId,
                        principalTable: "AdvisoryRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AdvisoryInstanceAssessments_DatabaseInstances_DatabaseInstanceId",
                        column: x => x.DatabaseInstanceId,
                        principalTable: "DatabaseInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApprovedPatchBaselines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Engine = table.Column<int>(type: "int", nullable: false),
                    Family = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TargetReleaseCatalogEntryId = table.Column<int>(type: "int", nullable: false),
                    ReviewedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovedPatchBaselines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApprovedPatchBaselines_ReleaseCatalogEntries_TargetReleaseCatalogEntryId",
                        column: x => x.TargetReleaseCatalogEntryId,
                        principalTable: "ReleaseCatalogEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReleaseCatalogRevisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReleaseCatalogEntryId = table.Column<int>(type: "int", nullable: false),
                    RevisedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevisedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    ChangeSummary = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseCatalogRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReleaseCatalogRevisions_ReleaseCatalogEntries_ReleaseCatalogEntryId",
                        column: x => x.ReleaseCatalogEntryId,
                        principalTable: "ReleaseCatalogEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupportRunwayDecisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DatabaseInstanceId = table.Column<int>(type: "int", nullable: false),
                    ReleaseCatalogEntryId = table.Column<int>(type: "int", nullable: true),
                    ActionType = table.Column<int>(type: "int", nullable: false),
                    ActionReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DecidedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportRunwayDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupportRunwayDecisions_DatabaseInstances_DatabaseInstanceId",
                        column: x => x.DatabaseInstanceId,
                        principalTable: "DatabaseInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SupportRunwayDecisions_ReleaseCatalogEntries_ReleaseCatalogEntryId",
                        column: x => x.ReleaseCatalogEntryId,
                        principalTable: "ReleaseCatalogEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ClusterNodeVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DatabaseInstanceId = table.Column<int>(type: "int", nullable: false),
                    VersionIdentityId = table.Column<int>(type: "int", nullable: true),
                    NodeName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RawVersionText = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ResolutionStatus = table.Column<int>(type: "int", nullable: false),
                    Family = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Major = table.Column<int>(type: "int", nullable: true),
                    Minor = table.Column<int>(type: "int", nullable: true),
                    Build = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ObservedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClusterNodeVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClusterNodeVersions_DatabaseInstances_DatabaseInstanceId",
                        column: x => x.DatabaseInstanceId,
                        principalTable: "DatabaseInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ClusterNodeVersions_VersionIdentities_VersionIdentityId",
                        column: x => x.VersionIdentityId,
                        principalTable: "VersionIdentities",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdvisoryInstanceAssessments_AdvisoryRecordId_DatabaseInstanceId",
                table: "AdvisoryInstanceAssessments",
                columns: new[] { "AdvisoryRecordId", "DatabaseInstanceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdvisoryInstanceAssessments_DatabaseInstanceId",
                table: "AdvisoryInstanceAssessments",
                column: "DatabaseInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_AdvisoryRecords_Engine_Family",
                table: "AdvisoryRecords",
                columns: new[] { "Engine", "Family" });

            migrationBuilder.CreateIndex(
                name: "IX_AdvisoryRecords_VendorAdvisoryId",
                table: "AdvisoryRecords",
                column: "VendorAdvisoryId");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovedPatchBaselines_Engine_Family_IsActive",
                table: "ApprovedPatchBaselines",
                columns: new[] { "Engine", "Family", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovedPatchBaselines_TargetReleaseCatalogEntryId",
                table: "ApprovedPatchBaselines",
                column: "TargetReleaseCatalogEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_ClusterNodeVersions_DatabaseInstanceId",
                table: "ClusterNodeVersions",
                column: "DatabaseInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_ClusterNodeVersions_VersionIdentityId",
                table: "ClusterNodeVersions",
                column: "VersionIdentityId");

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseCatalogEntries_Engine_Family_Major_Minor",
                table: "ReleaseCatalogEntries",
                columns: new[] { "Engine", "Family", "Major", "Minor" });

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseCatalogRevisions_ReleaseCatalogEntryId",
                table: "ReleaseCatalogRevisions",
                column: "ReleaseCatalogEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportRunwayDecisions_DatabaseInstanceId_DecidedAtUtc",
                table: "SupportRunwayDecisions",
                columns: new[] { "DatabaseInstanceId", "DecidedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportRunwayDecisions_ReleaseCatalogEntryId",
                table: "SupportRunwayDecisions",
                column: "ReleaseCatalogEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_VersionIdentities_DatabaseInstanceId",
                table: "VersionIdentities",
                column: "DatabaseInstanceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VersionIdentities_Family_Major_Minor",
                table: "VersionIdentities",
                columns: new[] { "Family", "Major", "Minor" });

            migrationBuilder.CreateIndex(
                name: "IX_VersionIdentities_ResolutionStatus",
                table: "VersionIdentities",
                column: "ResolutionStatus");

            migrationBuilder.CreateIndex(
                name: "IX_VersionObservations_DatabaseInstanceId_ObservedAtUtc",
                table: "VersionObservations",
                columns: new[] { "DatabaseInstanceId", "ObservedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdvisoryInstanceAssessments");

            migrationBuilder.DropTable(
                name: "ApprovedPatchBaselines");

            migrationBuilder.DropTable(
                name: "ClusterNodeVersions");

            migrationBuilder.DropTable(
                name: "ReleaseCatalogRevisions");

            migrationBuilder.DropTable(
                name: "SupportRunwayDecisions");

            migrationBuilder.DropTable(
                name: "VersionObservations");

            migrationBuilder.DropTable(
                name: "AdvisoryRecords");

            migrationBuilder.DropTable(
                name: "VersionIdentities");

            migrationBuilder.DropTable(
                name: "ReleaseCatalogEntries");
        }
    }
}
