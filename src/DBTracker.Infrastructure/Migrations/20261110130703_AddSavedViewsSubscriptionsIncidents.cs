using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DBTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSavedViewsSubscriptionsIncidents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Company",
                table: "Notifications",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

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
                name: "IX_SavedViews_UserId_ViewType",
                table: "SavedViews",
                columns: new[] { "UserId", "ViewType" });

            migrationBuilder.CreateIndex(
                name: "IX_SavedViews_UserId_ViewType_Name",
                table: "SavedViews",
                columns: new[] { "UserId", "ViewType", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Incidents");

            migrationBuilder.DropTable(
                name: "NotificationReadStates");

            migrationBuilder.DropTable(
                name: "NotificationSubscriptions");

            migrationBuilder.DropTable(
                name: "SavedViews");

            migrationBuilder.DropColumn(
                name: "Company",
                table: "Notifications");
        }
    }
}
