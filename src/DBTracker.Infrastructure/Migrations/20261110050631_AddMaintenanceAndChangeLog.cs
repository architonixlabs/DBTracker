using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DBTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMaintenanceAndChangeLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MaintenanceReason",
                table: "DatabaseInstances",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "MaintenanceUntilUtc",
                table: "DatabaseInstances",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InstanceChangeLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DatabaseInstanceId = table.Column<int>(type: "int", nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChangedBy = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    FieldName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OldValue = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    NewValue = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InstanceChangeLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InstanceChangeLogs_DatabaseInstances_DatabaseInstanceId",
                        column: x => x.DatabaseInstanceId,
                        principalTable: "DatabaseInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InstanceChangeLogs_DatabaseInstanceId_ChangedAtUtc",
                table: "InstanceChangeLogs",
                columns: new[] { "DatabaseInstanceId", "ChangedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InstanceChangeLogs");

            migrationBuilder.DropColumn(
                name: "MaintenanceReason",
                table: "DatabaseInstances");

            migrationBuilder.DropColumn(
                name: "MaintenanceUntilUtc",
                table: "DatabaseInstances");
        }
    }
}
