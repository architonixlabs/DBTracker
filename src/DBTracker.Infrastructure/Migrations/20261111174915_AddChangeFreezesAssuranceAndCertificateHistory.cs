using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DBTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChangeFreezesAssuranceAndCertificateHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RequiresEncryptedCopies",
                table: "RecoveryObjectives",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresImmutableCopy",
                table: "RecoveryObjectives",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresOffsiteCopy",
                table: "RecoveryObjectives",
                type: "bit",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccountableOwnerEmail",
                table: "ReadinessExceptions",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReviewerUserId",
                table: "ChangeValidationSignoffs",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "OwnerEmail",
                table: "CertificateDependencies",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "EncryptionConfirmed",
                table: "BackupCopyLocations",
                type: "bit",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CertificateRenewalEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CertificateDependencyId = table.Column<int>(type: "int", nullable: false),
                    EventType = table.Column<int>(type: "int", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PreviousExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NewExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Evidence = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ReplicasOrClientsUpdated = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    RecordedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RecordedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CertificateRenewalEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CertificateRenewalEvents_CertificateDependencies_CertificateDependencyId",
                        column: x => x.CertificateDependencyId,
                        principalTable: "CertificateDependencies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChangeFreezePeriods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    StartUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AppliesToEnvironment = table.Column<int>(type: "int", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CancelledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CancelledBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeFreezePeriods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InstanceCapacityLimits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DatabaseInstanceId = table.Column<int>(type: "int", nullable: false),
                    CapacityLimitGb = table.Column<double>(type: "float", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstanceCapacityLimits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstanceCapacityLimits_DatabaseInstances_DatabaseInstanceId",
                        column: x => x.DatabaseInstanceId,
                        principalTable: "DatabaseInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MaintenanceCheckRequirements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DatabaseInstanceId = table.Column<int>(type: "int", nullable: false),
                    CheckType = table.Column<int>(type: "int", nullable: false),
                    NodeOrDeploymentLabel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ExpectedIntervalHours = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceCheckRequirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceCheckRequirements_DatabaseInstances_DatabaseInstanceId",
                        column: x => x.DatabaseInstanceId,
                        principalTable: "DatabaseInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CertificateRenewalEvents_CertificateDependencyId_OccurredAtUtc",
                table: "CertificateRenewalEvents",
                columns: new[] { "CertificateDependencyId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ChangeFreezePeriods_StartUtc_EndUtc",
                table: "ChangeFreezePeriods",
                columns: new[] { "StartUtc", "EndUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_InstanceCapacityLimits_DatabaseInstanceId",
                table: "InstanceCapacityLimits",
                column: "DatabaseInstanceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceCheckRequirements_DatabaseInstanceId_CheckType_NodeOrDeploymentLabel",
                table: "MaintenanceCheckRequirements",
                columns: new[] { "DatabaseInstanceId", "CheckType", "NodeOrDeploymentLabel" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CertificateRenewalEvents");

            migrationBuilder.DropTable(
                name: "ChangeFreezePeriods");

            migrationBuilder.DropTable(
                name: "InstanceCapacityLimits");

            migrationBuilder.DropTable(
                name: "MaintenanceCheckRequirements");

            migrationBuilder.DropColumn(
                name: "RequiresEncryptedCopies",
                table: "RecoveryObjectives");

            migrationBuilder.DropColumn(
                name: "RequiresImmutableCopy",
                table: "RecoveryObjectives");

            migrationBuilder.DropColumn(
                name: "RequiresOffsiteCopy",
                table: "RecoveryObjectives");

            migrationBuilder.DropColumn(
                name: "AccountableOwnerEmail",
                table: "ReadinessExceptions");

            migrationBuilder.DropColumn(
                name: "ReviewerUserId",
                table: "ChangeValidationSignoffs");

            migrationBuilder.DropColumn(
                name: "OwnerEmail",
                table: "CertificateDependencies");

            migrationBuilder.DropColumn(
                name: "EncryptionConfirmed",
                table: "BackupCopyLocations");
        }
    }
}
