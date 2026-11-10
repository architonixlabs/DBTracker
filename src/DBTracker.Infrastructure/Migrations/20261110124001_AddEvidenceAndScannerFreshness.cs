using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DBTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEvidenceAndScannerFreshness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BackupFileNamePatterns",
                table: "DatabaseInstances",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<long>(
                name: "ObservedFileBytes",
                table: "BackupRecords",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BackupRecords_DatabaseInstanceId_SourceFile_ObservedFileBytes",
                table: "BackupRecords",
                columns: new[] { "DatabaseInstanceId", "SourceFile", "ObservedFileBytes" },
                unique: true,
                filter: "[Source] = 1 AND [ObservedFileBytes] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BackupRecords_DatabaseInstanceId_SourceFile_ObservedFileBytes",
                table: "BackupRecords");

            migrationBuilder.DropColumn(
                name: "BackupFileNamePatterns",
                table: "DatabaseInstances");

            migrationBuilder.DropColumn(
                name: "ObservedFileBytes",
                table: "BackupRecords");
        }
    }
}
