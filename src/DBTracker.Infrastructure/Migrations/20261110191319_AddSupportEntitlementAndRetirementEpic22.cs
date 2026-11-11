using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DBTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupportEntitlementAndRetirementEpic22 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SourceCostEstimateId",
                table: "CampaignAlternatives",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RetirementChecklists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DatabaseInstanceId = table.Column<int>(type: "int", nullable: false),
                    MigrationCampaignId = table.Column<int>(type: "int", nullable: true),
                    LegalHoldReviewed = table.Column<bool>(type: "bit", nullable: false),
                    LegalHoldReviewNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    AppOwnerSignoffBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    AppOwnerSignoffAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PlatformSignoffBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    PlatformSignoffAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExternalCleanupNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CostContractReviewed = table.Column<bool>(type: "bit", nullable: false),
                    CostContractReviewNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    FinalBackupEvidenceNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    RetiredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RetiredBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RetirementChecklists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RetirementChecklists_DatabaseInstances_DatabaseInstanceId",
                        column: x => x.DatabaseInstanceId,
                        principalTable: "DatabaseInstances",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RetirementChecklists_MigrationCampaigns_MigrationCampaignId",
                        column: x => x.MigrationCampaignId,
                        principalTable: "MigrationCampaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupportContracts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Vendor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    CoverageType = table.Column<int>(type: "int", nullable: false),
                    CoverageStartUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CoverageEndUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    OwnerTeamId = table.Column<int>(type: "int", nullable: true),
                    OwnerName = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    RenewalEvidenceReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RenewalEvidenceRecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportContracts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupportContracts_Teams_OwnerTeamId",
                        column: x => x.OwnerTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "EditionCostEstimates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DatabaseInstanceId = table.Column<int>(type: "int", nullable: false),
                    SupportContractId = table.Column<int>(type: "int", nullable: true),
                    EstimateType = table.Column<int>(type: "int", nullable: false),
                    TargetEditionOrPath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    LicensingConstraintsNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    EstimatedAmount = table.Column<decimal>(type: "decimal(14,2)", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    EstimateDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Assumptions = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ReviewedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EditionCostEstimates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EditionCostEstimates_DatabaseInstances_DatabaseInstanceId",
                        column: x => x.DatabaseInstanceId,
                        principalTable: "DatabaseInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EditionCostEstimates_SupportContracts_SupportContractId",
                        column: x => x.SupportContractId,
                        principalTable: "SupportContracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SupportContractAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupportContractId = table.Column<int>(type: "int", nullable: false),
                    DatabaseInstanceId = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportContractAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupportContractAssignments_DatabaseInstances_DatabaseInstanceId",
                        column: x => x.DatabaseInstanceId,
                        principalTable: "DatabaseInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SupportContractAssignments_SupportContracts_SupportContractId",
                        column: x => x.SupportContractId,
                        principalTable: "SupportContracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignAlternatives_SourceCostEstimateId",
                table: "CampaignAlternatives",
                column: "SourceCostEstimateId");

            migrationBuilder.CreateIndex(
                name: "IX_EditionCostEstimates_DatabaseInstanceId",
                table: "EditionCostEstimates",
                column: "DatabaseInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_EditionCostEstimates_SupportContractId",
                table: "EditionCostEstimates",
                column: "SupportContractId");

            migrationBuilder.CreateIndex(
                name: "IX_RetirementChecklists_DatabaseInstanceId",
                table: "RetirementChecklists",
                column: "DatabaseInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_RetirementChecklists_MigrationCampaignId",
                table: "RetirementChecklists",
                column: "MigrationCampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportContractAssignments_DatabaseInstanceId",
                table: "SupportContractAssignments",
                column: "DatabaseInstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportContractAssignments_SupportContractId_DatabaseInstanceId",
                table: "SupportContractAssignments",
                columns: new[] { "SupportContractId", "DatabaseInstanceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupportContracts_CoverageEndUtc",
                table: "SupportContracts",
                column: "CoverageEndUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SupportContracts_OwnerTeamId",
                table: "SupportContracts",
                column: "OwnerTeamId");

            migrationBuilder.AddForeignKey(
                name: "FK_CampaignAlternatives_EditionCostEstimates_SourceCostEstimateId",
                table: "CampaignAlternatives",
                column: "SourceCostEstimateId",
                principalTable: "EditionCostEstimates",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CampaignAlternatives_EditionCostEstimates_SourceCostEstimateId",
                table: "CampaignAlternatives");

            migrationBuilder.DropTable(
                name: "EditionCostEstimates");

            migrationBuilder.DropTable(
                name: "RetirementChecklists");

            migrationBuilder.DropTable(
                name: "SupportContractAssignments");

            migrationBuilder.DropTable(
                name: "SupportContracts");

            migrationBuilder.DropIndex(
                name: "IX_CampaignAlternatives_SourceCostEstimateId",
                table: "CampaignAlternatives");

            migrationBuilder.DropColumn(
                name: "SourceCostEstimateId",
                table: "CampaignAlternatives");
        }
    }
}
