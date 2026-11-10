using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DBTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUsabilityEpic17 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TeamId",
                table: "NotificationSubscriptions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OwnerTeamId",
                table: "Notifications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AppliedPolicyId",
                table: "DatabaseInstances",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AppliedPolicyVersion",
                table: "DatabaseInstances",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BackupContactEmail",
                table: "DatabaseInstances",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BackupContactName",
                table: "DatabaseInstances",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BackupContactPhone",
                table: "DatabaseInstances",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Cluster",
                table: "DatabaseInstances",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "OwnerTeamId",
                table: "DatabaseInstances",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OwnershipConfirmedAtUtc",
                table: "DatabaseInstances",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnershipConfirmedBy",
                table: "DatabaseInstances",
                type: "nvarchar(320)",
                maxLength: 320,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Region",
                table: "DatabaseInstances",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ReplicaSet",
                table: "DatabaseInstances",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "BackupPolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    ExpectedBackupIntervalHours = table.Column<int>(type: "int", nullable: false),
                    RetentionPolicyDays = table.Column<int>(type: "int", nullable: true),
                    RestoreTestIntervalDays = table.Column<int>(type: "int", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupPolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContactCorrectionRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DatabaseInstanceId = table.Column<int>(type: "int", nullable: false),
                    RequestedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RequestedByName = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    RequestedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProposedContactName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProposedContactEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    ProposedContactPhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProposedBackupContactName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ProposedBackupContactEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    ProposedBackupContactPhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReviewedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ReviewedByName = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContactCorrectionRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContactCorrectionRequests_DatabaseInstances_DatabaseInstanceId",
                        column: x => x.DatabaseInstanceId,
                        principalTable: "DatabaseInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Teams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PrimaryContactName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PrimaryContactEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    BackupContactName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    BackupContactEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TeamMemberships",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TeamId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamMemberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamMemberships_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationSubscriptions_TeamId",
                table: "NotificationSubscriptions",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_OwnerTeamId",
                table: "Notifications",
                column: "OwnerTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_DatabaseInstances_AppliedPolicyId",
                table: "DatabaseInstances",
                column: "AppliedPolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_DatabaseInstances_Cluster",
                table: "DatabaseInstances",
                column: "Cluster");

            migrationBuilder.CreateIndex(
                name: "IX_DatabaseInstances_OwnerTeamId",
                table: "DatabaseInstances",
                column: "OwnerTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_DatabaseInstances_Region",
                table: "DatabaseInstances",
                column: "Region");

            migrationBuilder.CreateIndex(
                name: "IX_BackupPolicies_Name",
                table: "BackupPolicies",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContactCorrectionRequests_DatabaseInstanceId_Status",
                table: "ContactCorrectionRequests",
                columns: new[] { "DatabaseInstanceId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamMemberships_TeamId_UserId",
                table: "TeamMemberships",
                columns: new[] { "TeamId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeamMemberships_UserId",
                table: "TeamMemberships",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_Name",
                table: "Teams",
                column: "Name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DatabaseInstances_BackupPolicies_AppliedPolicyId",
                table: "DatabaseInstances",
                column: "AppliedPolicyId",
                principalTable: "BackupPolicies",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_DatabaseInstances_Teams_OwnerTeamId",
                table: "DatabaseInstances",
                column: "OwnerTeamId",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_NotificationSubscriptions_Teams_TeamId",
                table: "NotificationSubscriptions",
                column: "TeamId",
                principalTable: "Teams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DatabaseInstances_BackupPolicies_AppliedPolicyId",
                table: "DatabaseInstances");

            migrationBuilder.DropForeignKey(
                name: "FK_DatabaseInstances_Teams_OwnerTeamId",
                table: "DatabaseInstances");

            migrationBuilder.DropForeignKey(
                name: "FK_NotificationSubscriptions_Teams_TeamId",
                table: "NotificationSubscriptions");

            migrationBuilder.DropTable(
                name: "BackupPolicies");

            migrationBuilder.DropTable(
                name: "ContactCorrectionRequests");

            migrationBuilder.DropTable(
                name: "TeamMemberships");

            migrationBuilder.DropTable(
                name: "Teams");

            migrationBuilder.DropIndex(
                name: "IX_NotificationSubscriptions_TeamId",
                table: "NotificationSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_OwnerTeamId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_DatabaseInstances_AppliedPolicyId",
                table: "DatabaseInstances");

            migrationBuilder.DropIndex(
                name: "IX_DatabaseInstances_Cluster",
                table: "DatabaseInstances");

            migrationBuilder.DropIndex(
                name: "IX_DatabaseInstances_OwnerTeamId",
                table: "DatabaseInstances");

            migrationBuilder.DropIndex(
                name: "IX_DatabaseInstances_Region",
                table: "DatabaseInstances");

            migrationBuilder.DropColumn(
                name: "TeamId",
                table: "NotificationSubscriptions");

            migrationBuilder.DropColumn(
                name: "OwnerTeamId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "AppliedPolicyId",
                table: "DatabaseInstances");

            migrationBuilder.DropColumn(
                name: "AppliedPolicyVersion",
                table: "DatabaseInstances");

            migrationBuilder.DropColumn(
                name: "BackupContactEmail",
                table: "DatabaseInstances");

            migrationBuilder.DropColumn(
                name: "BackupContactName",
                table: "DatabaseInstances");

            migrationBuilder.DropColumn(
                name: "BackupContactPhone",
                table: "DatabaseInstances");

            migrationBuilder.DropColumn(
                name: "Cluster",
                table: "DatabaseInstances");

            migrationBuilder.DropColumn(
                name: "OwnerTeamId",
                table: "DatabaseInstances");

            migrationBuilder.DropColumn(
                name: "OwnershipConfirmedAtUtc",
                table: "DatabaseInstances");

            migrationBuilder.DropColumn(
                name: "OwnershipConfirmedBy",
                table: "DatabaseInstances");

            migrationBuilder.DropColumn(
                name: "Region",
                table: "DatabaseInstances");

            migrationBuilder.DropColumn(
                name: "ReplicaSet",
                table: "DatabaseInstances");
        }
    }
}
