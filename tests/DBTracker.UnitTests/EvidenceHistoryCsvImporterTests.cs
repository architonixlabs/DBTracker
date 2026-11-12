using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

/// <summary>G23: backup and restore history import with explicit zones, repeat-safe matching and Imported provenance.</summary>
public class EvidenceHistoryCsvImporterTests
{
    private static readonly DateTime Now = new(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);
    private const string Header = "Kind,InstanceName,EventTime,TimeZone,Type,Status,SizeMb,DurationMinutes,Notes,PerformedBy,EvidenceReference\n";

    private static readonly Dictionary<string, int> Instances = new(StringComparer.OrdinalIgnoreCase) { ["ARX-SQL-01"] = 7, ["ARX-PG-02"] = 9 };

    private static HistoryImportPreview Parse(string body, IEnumerable<HistoryExistingEvent>? existing = null, Dictionary<string, string>? blocked = null) =>
        EvidenceHistoryCsvImporter.ParseForImport(Header + body, Instances, blocked, existing ?? [], Now, "priya.nair@example.com", "history.csv");

    [Fact]
    public void BackupRow_BecomesAnImportedRecord_WithTheImporterAndStatedZone()
    {
        var preview = Parse("Backup,arx-sql-01,2026-10-01 02:00,Europe/London,Full,Success,51200,42,Nightly,,\n");

        Assert.Empty(preview.Errors);
        var row = Assert.Single(preview.Rows);
        Assert.Equal(ImportRowPlan.Create, row.Plan);
        var b = row.Backup!;
        Assert.Equal(7, b.DatabaseInstanceId);
        Assert.Equal(new DateTime(2026, 10, 1, 1, 0, 0, DateTimeKind.Utc), b.TimestampUtc);
        Assert.Equal(BackupSource.Imported, b.Source);
        Assert.Equal("priya.nair@example.com", b.RecordedByUserName);
        Assert.Equal("Europe/London", b.OccurredTimeZone);
        Assert.Equal(Now, b.CreatedAtUtc);
        Assert.Equal(51200, b.SizeMb);
        Assert.Equal(42, b.DurationMinutes);
        Assert.Equal("history.csv", b.SourceFile);
    }

    [Fact]
    public void RestoreRow_BecomesAnImportedRestore_WithEvidenceReference()
    {
        var preview = Parse("Restore,ARX-PG-02,2026-10-15T09:30:00Z,,Test,Success,,35,Drill,Priya Nair,CHG-1042\n");

        var r = Assert.Single(preview.Rows).Restore!;
        Assert.Equal(9, r.DatabaseInstanceId);
        Assert.Equal(BackupSource.Imported, r.Source);
        Assert.Equal(RestoreReason.Test, r.Reason);
        Assert.Equal(RestoreOutcome.Success, r.Outcome);
        Assert.Equal("CHG-1042", r.EvidenceReference);
        Assert.Equal("Priya Nair", r.PerformedBy);
        Assert.Equal("UTC", r.OccurredTimeZone);
    }

    [Fact]
    public void ImportedRestore_NeverCountsAsVerified()
    {
        var r = Assert.Single(Parse("Restore,ARX-PG-02,2026-10-15T09:30:00Z,,Test,Success,,,,,\n").Rows).Restore!;

        Assert.False(RestoreEvidence.CountsAsVerified(r));
    }

    [Fact]
    public void RowAlreadyStored_IsSkipped_SoReimportingAddsNothing()
    {
        var existing = new[] { new HistoryExistingEvent(7, HistoryEventKind.Backup, new DateTime(2026, 10, 1, 1, 0, 0, 400, DateTimeKind.Utc), "Full", "Success") };

        var preview = Parse("Backup,ARX-SQL-01,2026-10-01T01:00:00Z,,Full,Success,,,,,\n", existing);

        Assert.Equal(ImportRowPlan.Skip, Assert.Single(preview.Rows).Plan);
        Assert.Equal(0, preview.CreateCount);
    }

    [Fact]
    public void StoredEntryWithADifferentStatus_IsAmbiguous_NotASecondEntry()
    {
        var existing = new[] { new HistoryExistingEvent(7, HistoryEventKind.Backup, new DateTime(2026, 10, 1, 1, 0, 0, DateTimeKind.Utc), "Full", "Failed") };

        var row = Assert.Single(Parse("Backup,ARX-SQL-01,2026-10-01T01:00:00Z,,Full,Success,,,,,\n", existing).Rows);

        Assert.Equal(ImportRowPlan.Ambiguous, row.Plan);
        Assert.Contains("already recorded as Failed", row.Detail);
    }

    [Fact]
    public void SameEventTwiceInOneFile_SecondIsAmbiguous()
    {
        // Same instant stated in two zones.
        var preview = Parse("Backup,ARX-SQL-01,2026-10-01 02:00,Europe/London,Full,Success,,,,,\nBackup,ARX-SQL-01,2026-10-01T01:00:00Z,,Full,Success,,,,,\n");

        Assert.Equal(1, preview.CreateCount);
        Assert.Contains("Repeats row 2", preview.Rows[1].Detail);
    }

    [Fact]
    public void DifferentTypeAtTheSameTime_IsADifferentEvent()
    {
        var preview = Parse("Backup,ARX-SQL-01,2026-10-01T01:00:00Z,,Full,Success,,,,,\nBackup,ARX-SQL-01,2026-10-01T01:00:00Z,,Log,Success,,,,,\n");

        Assert.Equal(2, preview.CreateCount);
        Assert.Equal(BackupType.TransactionLog, preview.Rows[1].Backup!.Type);
    }

    [Theory]
    [InlineData("Backup,NOPE,2026-10-01T01:00:00Z,,Full,Success,,,,,", "No active instance is named 'NOPE'")]
    [InlineData("Backup,ARX-SQL-01,2026-10-01 01:00,,Full,Success,,,,,", "has no time zone")]
    [InlineData("Backup,ARX-SQL-01,2026-12-01T01:00:00Z,,Full,Success,,,,,", "can't be in the future")]
    [InlineData("Backup,ARX-SQL-01,2026-10-01T01:00:00Z,,Weekly,Success,,,,,", "isn't a backup type")]
    [InlineData("Backup,ARX-SQL-01,2026-10-01T01:00:00Z,,Full,0,,,,,", "isn't a backup status")]
    [InlineData("Backup,ARX-SQL-01,2026-10-01T01:00:00Z,,Full,Success,-5,,,,", "SizeMb can't be negative")]
    [InlineData("Backup,ARX-SQL-01,2026-10-01T01:00:00Z,,Full,Success,,,,Priya,", "PerformedBy applies to restores only")]
    [InlineData("Restore,ARX-SQL-01,2026-10-01T01:00:00Z,,Test,Success,100,,,,", "SizeMb applies to backups only")]
    [InlineData("Restore,ARX-SQL-01,2026-10-01T01:00:00Z,,Full,Success,,,,,", "isn't a restore reason")]
    [InlineData("Snapshot,ARX-SQL-01,2026-10-01T01:00:00Z,,Full,Success,,,,,", "must be Backup or Restore")]
    public void InvalidRows_AreErrors_WithAReasonToFix(string line, string expected)
    {
        var preview = Parse(line + "\n");

        Assert.Empty(preview.Rows);
        Assert.Contains(expected, Assert.Single(preview.Errors).Message);
    }

    [Fact]
    public void BlockedName_ShowsTheReason()
    {
        var blocked = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["OLD-DB"] = "'OLD-DB' is archived." };

        var preview = Parse("Backup,OLD-DB,2026-10-01T01:00:00Z,,Full,Success,,,,,\n", blocked: blocked);

        Assert.Contains("archived", Assert.Single(preview.Errors).Message);
    }

    [Fact]
    public void MissingRequiredHeader_IsOneHeaderError()
    {
        var preview = EvidenceHistoryCsvImporter.ParseForImport("Kind,InstanceName\nBackup,ARX-SQL-01\n", Instances, null, [], Now, "x");

        Assert.Equal(1, Assert.Single(preview.Errors).RowNumber);
        Assert.Contains("EventTime", preview.Errors[0].Message);
    }

    [Fact]
    public void Template_ImportsNothingAsDownloaded_AndDocumentsEveryColumn()
    {
        var template = EvidenceHistoryCsvImporter.TemplateCsv();

        var preview = EvidenceHistoryCsvImporter.ParseForImport(template, Instances, null, [], Now, "x");
        Assert.Empty(preview.Rows);
        Assert.Empty(preview.Errors);
        Assert.Equal(EvidenceHistoryCsvImporter.Columns, EvidenceHistoryCsvImporter.ColumnGuide.Select(c => c.Name));
        Assert.Contains("# Status (required): Backup: Success | Failed | Missed", template);
    }

    [Fact]
    public void TemplateExampleRows_WithHashRemoved_ImportCleanly()
    {
        var lines = EvidenceHistoryCsvImporter.TemplateCsv().Split('\n');
        var examples = lines.Where(l => l.StartsWith("#Backup,") || l.StartsWith("#Restore,")).Select(l => l.TrimStart('#'));
        var csv = lines[0] + "\n" + string.Join("\n", examples) + "\n";
        var known = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["EXAMPLE-SQL-01"] = 1 };

        var preview = EvidenceHistoryCsvImporter.ParseForImport(csv, known, null, [], Now, "x");

        Assert.Empty(preview.Errors);
        Assert.Equal(1, preview.BackupCreateCount);
        Assert.Equal(1, preview.RestoreCreateCount);
    }

    [Fact]
    public void ImportedBackup_IsClassifiedImported_EvenAfterAVerifiedRestore()
    {
        var backup = new BackupRecord { Status = BackupStatus.Success, Source = BackupSource.Imported, TimestampUtc = Now.AddDays(-2) };
        var restore = new RestoreEvent { Outcome = RestoreOutcome.Success, TimestampUtc = Now.AddDays(-1) };

        Assert.Equal(EvidenceLevel.Imported, BackupEvidenceClassifier.Classify(backup, [restore]));
    }

    [Fact]
    public void ImportedRestore_DoesNotPromoteABackupToRestoreTested()
    {
        var backup = new BackupRecord { Status = BackupStatus.Success, Source = BackupSource.Manual, TimestampUtc = Now.AddDays(-2) };
        var imported = new RestoreEvent { Outcome = RestoreOutcome.Success, Source = BackupSource.Imported, TimestampUtc = Now.AddDays(-1) };

        Assert.Equal(EvidenceLevel.ReportedSuccess, BackupEvidenceClassifier.Classify(backup, [imported]));
    }

    [Fact]
    public void RestoreTestingReport_IgnoresImportedRestores()
    {
        var input = new RestoreTestingInput("ARX", "Co", "Dept", InstanceRole.Active, EnvironmentTier.Production,
            [new RestoreEvent { Outcome = RestoreOutcome.Success, Source = BackupSource.Imported, TimestampUtc = Now.AddDays(-1) }]);

        var row = Assert.Single(RestoreTestingReportBuilder.Build([input], 90, Now));

        Assert.False(row.RestoreTested);
        Assert.Equal(RestoreTestRecency.NeverTested, row.Recency);
    }
}
