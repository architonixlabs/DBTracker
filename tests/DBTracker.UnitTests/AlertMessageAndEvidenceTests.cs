using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

/// <summary>U04: evidence levels carried into alerts, report rows and the management report.</summary>
public class AlertMessageAndEvidenceTests
{
    private static readonly DateTime Now = new(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Alert_NamesLastSuccessAndItsEvidence()
    {
        var message = AlertMessageBuilder.BuildAlert("SALES-SQL", BackupHealth.Overdue, Now.AddHours(-30), EvidenceLevel.Discovered);
        Assert.Equal("SALES-SQL is now overdue for backup. Last successful backup 2026-11-10 06:00 UTC, evidence: Discovered.", message);
    }

    [Fact]
    public void Escalation_KeepsLevelAndEvidence()
    {
        var message = AlertMessageBuilder.BuildAlert("SALES-SQL", BackupHealth.Failed, Now, EvidenceLevel.RestoreTested, escalationLevel: 2);
        Assert.StartsWith("SALES-SQL is still failing backups (escalation #2, unacknowledged).", message);
        Assert.Contains("evidence: Restore-tested", message);
    }

    [Fact]
    public void NeverBackedUp_SaysNoSuccess()
    {
        var message = AlertMessageBuilder.BuildAlert("NEW-DB", BackupHealth.NeverBackedUp, null, null);
        Assert.EndsWith("No successful backup on record.", message);
    }

    [Fact]
    public void Recovery_DescribesEvidence()
    {
        var message = AlertMessageBuilder.BuildRecovery("SALES-SQL", Now, EvidenceLevel.ReportedSuccess);
        Assert.StartsWith("SALES-SQL has recovered: backup health is on track again.", message);
        Assert.Contains("evidence: Reported success", message);
    }

    [Fact]
    public void LatestSuccess_IgnoresLaterFailuresAndFutureRecords()
    {
        var records = new List<BackupRecord>
        {
            new() { Id = 1, TimestampUtc = Now.AddHours(-5), Status = BackupStatus.Success, Source = BackupSource.Discovered },
            new() { Id = 2, TimestampUtc = Now.AddHours(-1), Status = BackupStatus.Failed, Source = BackupSource.Manual },
            new() { Id = 3, TimestampUtc = Now.AddHours(3), Status = BackupStatus.Success, Source = BackupSource.Manual },
        };

        Assert.Equal(EvidenceLevel.Discovered, BackupEvidenceClassifier.ClassifyLatestSuccess(records, [], Now));
    }

    [Fact]
    public void LatestSuccess_RestoreAfterItMakesItRestoreTested_ButNotAFutureRestore()
    {
        var records = new List<BackupRecord> { new() { Id = 1, TimestampUtc = Now.AddDays(-2), Status = BackupStatus.Success, Source = BackupSource.Manual } };

        var pastRestore = new RestoreEvent { TimestampUtc = Now.AddDays(-1), Outcome = RestoreOutcome.Success };
        var futureRestore = new RestoreEvent { TimestampUtc = Now.AddDays(1), Outcome = RestoreOutcome.Success };

        Assert.Equal(EvidenceLevel.RestoreTested, BackupEvidenceClassifier.ClassifyLatestSuccess(records, [pastRestore], Now));
        Assert.Equal(EvidenceLevel.ReportedSuccess, BackupEvidenceClassifier.ClassifyLatestSuccess(records, [futureRestore], Now));
    }

    [Fact]
    public void LatestSuccess_NullWithoutSuccess()
    {
        var records = new List<BackupRecord> { new() { Id = 1, TimestampUtc = Now.AddDays(-2), Status = BackupStatus.Missed } };
        Assert.Null(BackupEvidenceClassifier.ClassifyLatestSuccess(records, [], Now));
        Assert.Equal("", BackupEvidenceClassifier.DescribeOrEmpty(null));
    }

    [Theory]
    [InlineData(BackupStatus.Failed, BackupSource.Manual, null, null)]
    [InlineData(BackupStatus.Success, BackupSource.Discovered, null, EvidenceLevel.Discovered)]
    [InlineData(BackupStatus.Success, BackupSource.Manual, -1, EvidenceLevel.ReportedSuccess)]
    [InlineData(BackupStatus.Success, BackupSource.Discovered, 0, EvidenceLevel.RestoreTested)]
    [InlineData(BackupStatus.Success, BackupSource.Manual, 2, EvidenceLevel.RestoreTested)]
    public void FlatClassify_MatchesRecordRule(BackupStatus status, BackupSource source, int? restoreOffsetHours, EvidenceLevel? expected)
    {
        DateTime? lastRestore = restoreOffsetHours is { } h ? Now.AddHours(h) : null;
        Assert.Equal(expected, BackupEvidenceClassifier.Classify(status, source, Now, lastRestore));

        var record = new BackupRecord { TimestampUtc = Now, Status = status, Source = source };
        var restores = lastRestore is { } at ? new[] { new RestoreEvent { TimestampUtc = at, Outcome = RestoreOutcome.Success } } : [];
        Assert.Equal(expected, BackupEvidenceClassifier.Classify(record, restores));
    }

    [Fact]
    public void ManagementReport_ShowsEvidenceOnAtRiskRows()
    {
        var instance = new DatabaseInstance { Name = "HR-PG", Company = "Contoso", Department = "HR" };
        var rows = new[] { new ComplianceReportRow(instance, BackupHealth.Overdue, Now.AddDays(-3), false, EvidenceLevel.Discovered) };

        var body = ComplianceReportBuilder.BuildPlainTextBody(rows, Now);

        Assert.Contains("last backup: 2026-11-08 12:00 UTC (Discovered)", body);
    }
}
