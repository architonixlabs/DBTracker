using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DBTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WidenDiscoveredBackupSignature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BackupRecords_DatabaseInstanceId_SourceFile_ObservedFileBytes",
                table: "BackupRecords");

            migrationBuilder.CreateIndex(
                name: "IX_BackupRecords_DatabaseInstanceId_SourceFile_ObservedFileBytes_TimestampUtc",
                table: "BackupRecords",
                columns: new[] { "DatabaseInstanceId", "SourceFile", "ObservedFileBytes", "TimestampUtc" },
                unique: true,
                filter: "[Source] = 1 AND [ObservedFileBytes] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BackupRecords_DatabaseInstanceId_SourceFile_ObservedFileBytes_TimestampUtc",
                table: "BackupRecords");

            migrationBuilder.CreateIndex(
                name: "IX_BackupRecords_DatabaseInstanceId_SourceFile_ObservedFileBytes",
                table: "BackupRecords",
                columns: new[] { "DatabaseInstanceId", "SourceFile", "ObservedFileBytes" },
                unique: true,
                filter: "[Source] = 1 AND [ObservedFileBytes] IS NOT NULL");
        }
    }
}
