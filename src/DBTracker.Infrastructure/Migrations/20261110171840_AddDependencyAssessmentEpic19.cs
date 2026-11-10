using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DBTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDependencyAssessmentEpic19 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InstanceDependencies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DatabaseInstanceId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    OwnerTeamId = table.Column<int>(type: "int", nullable: true),
                    RecordedVersionText = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    VersionState = table.Column<int>(type: "int", nullable: false),
                    OsRuntimeConstraint = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ManagedProviderConstraint = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    EvidenceUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    EvidenceNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IsResolved = table.Column<bool>(type: "bit", nullable: false),
                    ResolutionNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    LastReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastReviewedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstanceDependencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstanceDependencies_DatabaseInstances_DatabaseInstanceId",
                        column: x => x.DatabaseInstanceId,
                        principalTable: "DatabaseInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InstanceDependencies_Teams_OwnerTeamId",
                        column: x => x.OwnerTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MigrationCampaigns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SourceLabel = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    TargetLabel = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ChosenMethod = table.Column<int>(type: "int", nullable: false),
                    AccountableDbaTeamId = table.Column<int>(type: "int", nullable: true),
                    AccountableAppTeamId = table.Column<int>(type: "int", nullable: true),
                    AccountablePlatformTeamId = table.Column<int>(type: "int", nullable: true),
                    Blockers = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Milestones = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    EstimatedEffortHours = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    EstimatedCost = table.Column<decimal>(type: "decimal(14,2)", nullable: true),
                    ChangeTicketReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    State = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MigrationCampaigns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MigrationCampaigns_Teams_AccountableAppTeamId",
                        column: x => x.AccountableAppTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MigrationCampaigns_Teams_AccountableDbaTeamId",
                        column: x => x.AccountableDbaTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_MigrationCampaigns_Teams_AccountablePlatformTeamId",
                        column: x => x.AccountablePlatformTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PathSupportRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Engine = table.Column<int>(type: "int", nullable: false),
                    AssessmentType = table.Column<int>(type: "int", nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false),
                    SourceFamily = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SourceBuildMin = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SourceBuildMax = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SourceEdition = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SourcePlatform = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SourceTopology = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TargetFamily = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TargetBuildMin = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TargetBuildMax = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TargetEdition = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TargetPlatform = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TargetTopology = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RequiredIntermediateVersions = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DowngradeConstraints = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    VendorGuidanceUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    VendorGuidanceNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ReviewedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PathSupportRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CampaignAlternatives",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MigrationCampaignId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    EstimatedEffortHours = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    EstimatedCost = table.Column<decimal>(type: "decimal(14,2)", nullable: true),
                    RiskNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    IsChosen = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignAlternatives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignAlternatives_MigrationCampaigns_MigrationCampaignId",
                        column: x => x.MigrationCampaignId,
                        principalTable: "MigrationCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CampaignApprovals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MigrationCampaignId = table.Column<int>(type: "int", nullable: false),
                    Scope = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Decision = table.Column<int>(type: "int", nullable: false),
                    ApproverName = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignApprovals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignApprovals_MigrationCampaigns_MigrationCampaignId",
                        column: x => x.MigrationCampaignId,
                        principalTable: "MigrationCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CampaignInstanceLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MigrationCampaignId = table.Column<int>(type: "int", nullable: false),
                    DatabaseInstanceId = table.Column<int>(type: "int", nullable: false),
                    AddedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignInstanceLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignInstanceLinks_DatabaseInstances_DatabaseInstanceId",
                        column: x => x.DatabaseInstanceId,
                        principalTable: "DatabaseInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CampaignInstanceLinks_MigrationCampaigns_MigrationCampaignId",
                        column: x => x.MigrationCampaignId,
                        principalTable: "MigrationCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CampaignStateTransitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MigrationCampaignId = table.Column<int>(type: "int", nullable: false),
                    FromState = table.Column<int>(type: "int", nullable: false),
                    ToState = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    TransitionedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TransitionedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignStateTransitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignStateTransitions_MigrationCampaigns_MigrationCampaignId",
                        column: x => x.MigrationCampaignId,
                        principalTable: "MigrationCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PathAssessments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DatabaseInstanceId = table.Column<int>(type: "int", nullable: false),
                    AssessmentType = table.Column<int>(type: "int", nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false),
                    SourceProduct = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SourceBuild = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SourceEdition = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SourcePlatform = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SourceTopology = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TargetProduct = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TargetBuild = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TargetEdition = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TargetPlatform = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TargetTopology = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    MatchedRuleId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    StatusReasoning = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    VendorGuidanceUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    VendorGuidanceNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RequiredIntermediateVersions = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DowngradeConstraints = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    EvaluatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EvaluatedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PathAssessments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PathAssessments_DatabaseInstances_DatabaseInstanceId",
                        column: x => x.DatabaseInstanceId,
                        principalTable: "DatabaseInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PathAssessments_PathSupportRules_MatchedRuleId",
                        column: x => x.MatchedRuleId,
                        principalTable: "PathSupportRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ReadinessChecklists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DatabaseInstanceId = table.Column<int>(type: "int", nullable: false),
                    PathAssessmentId = table.Column<int>(type: "int", nullable: true),
                    SourceLabel = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    TargetLabel = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadinessChecklists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReadinessChecklists_DatabaseInstances_DatabaseInstanceId",
                        column: x => x.DatabaseInstanceId,
                        principalTable: "DatabaseInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReadinessChecklists_PathAssessments_PathAssessmentId",
                        column: x => x.PathAssessmentId,
                        principalTable: "PathAssessments",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ReadinessCheckItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReadinessChecklistId = table.Column<int>(type: "int", nullable: false),
                    Area = table.Column<int>(type: "int", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Findings = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Reviewer = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SourceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    TargetReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RemediationOwner = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    RemediationNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    EvidenceExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsInvalidated = table.Column<bool>(type: "bit", nullable: false),
                    InvalidationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReadinessCheckItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReadinessCheckItems_ReadinessChecklists_ReadinessChecklistId",
                        column: x => x.ReadinessChecklistId,
                        principalTable: "ReadinessChecklists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignAlternatives_MigrationCampaignId",
                table: "CampaignAlternatives",
                column: "MigrationCampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignApprovals_MigrationCampaignId",
                table: "CampaignApprovals",
                column: "MigrationCampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignInstanceLinks_DatabaseInstanceId",
                table: "CampaignInstanceLinks",
                column: "DatabaseInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignInstanceLinks_MigrationCampaignId_DatabaseInstanceId",
                table: "CampaignInstanceLinks",
                columns: new[] { "MigrationCampaignId", "DatabaseInstanceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CampaignStateTransitions_MigrationCampaignId_TransitionedAtUtc",
                table: "CampaignStateTransitions",
                columns: new[] { "MigrationCampaignId", "TransitionedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_InstanceDependencies_DatabaseInstanceId_IsResolved",
                table: "InstanceDependencies",
                columns: new[] { "DatabaseInstanceId", "IsResolved" });

            migrationBuilder.CreateIndex(
                name: "IX_InstanceDependencies_Direction",
                table: "InstanceDependencies",
                column: "Direction");

            migrationBuilder.CreateIndex(
                name: "IX_InstanceDependencies_OwnerTeamId",
                table: "InstanceDependencies",
                column: "OwnerTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_MigrationCampaigns_AccountableAppTeamId",
                table: "MigrationCampaigns",
                column: "AccountableAppTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_MigrationCampaigns_AccountableDbaTeamId",
                table: "MigrationCampaigns",
                column: "AccountableDbaTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_MigrationCampaigns_AccountablePlatformTeamId",
                table: "MigrationCampaigns",
                column: "AccountablePlatformTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_MigrationCampaigns_State",
                table: "MigrationCampaigns",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_PathAssessments_DatabaseInstanceId_EvaluatedAtUtc",
                table: "PathAssessments",
                columns: new[] { "DatabaseInstanceId", "EvaluatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PathAssessments_MatchedRuleId",
                table: "PathAssessments",
                column: "MatchedRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_PathSupportRules_Engine_AssessmentType_Method",
                table: "PathSupportRules",
                columns: new[] { "Engine", "AssessmentType", "Method" });

            migrationBuilder.CreateIndex(
                name: "IX_ReadinessCheckItems_ReadinessChecklistId",
                table: "ReadinessCheckItems",
                column: "ReadinessChecklistId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadinessChecklists_DatabaseInstanceId",
                table: "ReadinessChecklists",
                column: "DatabaseInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_ReadinessChecklists_PathAssessmentId",
                table: "ReadinessChecklists",
                column: "PathAssessmentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampaignAlternatives");

            migrationBuilder.DropTable(
                name: "CampaignApprovals");

            migrationBuilder.DropTable(
                name: "CampaignInstanceLinks");

            migrationBuilder.DropTable(
                name: "CampaignStateTransitions");

            migrationBuilder.DropTable(
                name: "InstanceDependencies");

            migrationBuilder.DropTable(
                name: "ReadinessCheckItems");

            migrationBuilder.DropTable(
                name: "MigrationCampaigns");

            migrationBuilder.DropTable(
                name: "ReadinessChecklists");

            migrationBuilder.DropTable(
                name: "PathAssessments");

            migrationBuilder.DropTable(
                name: "PathSupportRules");
        }
    }
}
