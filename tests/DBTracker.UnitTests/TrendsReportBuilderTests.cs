using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class TrendsReportBuilderTests
{
    private static ComplianceSnapshotRow SnapshotRow(string name, BackupHealth health, bool restoreTested = false) =>
        new(name, "Acme", "Ops", InstanceRole.Active, EnvironmentTier.Production, DateTime.UtcNow.AddDays(-1), health, restoreTested);

    private static TrendPeriodInput Input(
        DateTime from, DateTime to,
        IReadOnlyList<ComplianceSnapshotRow>? snapshotRows = null,
        IReadOnlyList<TrendBackupEvent>? events = null,
        IReadOnlyList<DateTime>? restoreTests = null,
        IReadOnlyList<TrendOutstandingRisk>? risks = null) =>
        new(from, to, snapshotRows, snapshotRows is null ? null : to.AddMinutes(-1),
            events ?? [], restoreTests ?? [], risks ?? []);

    [Fact]
    public void NoSnapshot_IsFlaggedAsMissingEvidence_NotInterpolated()
    {
        var result = TrendsReportBuilder.BuildPeriod(Input(DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, snapshotRows: null));

        Assert.True(result.MissingEvidence);
        Assert.Null(result.InventoryCount);
        Assert.Null(result.OnTrackPct);
    }

    [Fact]
    public void WithSnapshot_ComputesInventoryDenominatorAndCoveragePercent()
    {
        var rows = new[]
        {
            SnapshotRow("A", BackupHealth.OnTrack),
            SnapshotRow("B", BackupHealth.OnTrack),
            SnapshotRow("C", BackupHealth.Failed),
            SnapshotRow("D", BackupHealth.Overdue),
        };
        var result = TrendsReportBuilder.BuildPeriod(Input(DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, snapshotRows: rows));

        Assert.False(result.MissingEvidence);
        Assert.Equal(4, result.InventoryCount);
        Assert.Equal(2, result.OnTrackCount);
        Assert.Equal(2, result.AtRiskCount);
        Assert.Equal(50.0, result.OnTrackPct);
        Assert.Equal(2, result.AtRiskRows.Count);
    }

    [Fact]
    public void RestoreTestedCoverage_ComesFromSnapshotRows()
    {
        var rows = new[] { SnapshotRow("A", BackupHealth.OnTrack, restoreTested: true), SnapshotRow("B", BackupHealth.OnTrack, restoreTested: false) };
        var result = TrendsReportBuilder.BuildPeriod(Input(DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, snapshotRows: rows));

        Assert.Equal(1, result.RestoreTestedCount);
        Assert.Equal(50.0, result.RestoreTestedPct);
    }

    [Fact]
    public void RepeatedFailures_RequireAtLeastThresholdFailuresInPeriod()
    {
        var events = new[]
        {
            new TrendBackupEvent("A", DateTime.UtcNow.AddDays(-5), BackupStatus.Failed, null, null),
            new TrendBackupEvent("A", DateTime.UtcNow.AddDays(-4), BackupStatus.Failed, null, null),
            new TrendBackupEvent("B", DateTime.UtcNow.AddDays(-3), BackupStatus.Failed, null, null),
        };
        var result = TrendsReportBuilder.BuildPeriod(Input(DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, events: events));

        Assert.Equal(1, result.RepeatedFailureInstanceCount);
        Assert.Equal("A", result.RepeatedFailureRows.Single().InstanceName);
        Assert.Equal(2, result.RepeatedFailureRows.Single().FailureCount);
    }

    [Fact]
    public void BackupDurationAndSize_AveragedFromRawEventsRegardlessOfMissingSnapshot()
    {
        var events = new[]
        {
            new TrendBackupEvent("A", DateTime.UtcNow.AddDays(-5), BackupStatus.Success, 100.0, 10),
            new TrendBackupEvent("A", DateTime.UtcNow.AddDays(-4), BackupStatus.Success, 300.0, 30),
        };
        var result = TrendsReportBuilder.BuildPeriod(Input(DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, snapshotRows: null, events: events));

        Assert.True(result.MissingEvidence);
        Assert.Equal(200.0, result.AvgBackupSizeMb);
        Assert.Equal(20.0, result.AvgBackupDurationMinutes);
        Assert.Equal(2, result.BackupEventCount);
    }

    [Fact]
    public void OutstandingRiskAge_AveragedFromIncidentOpenTimestamps()
    {
        var periodEnd = DateTime.UtcNow;
        var risks = new[]
        {
            new TrendOutstandingRisk("A", periodEnd.AddHours(-10), BackupHealth.Failed),
            new TrendOutstandingRisk("B", periodEnd.AddHours(-30), BackupHealth.Overdue),
        };
        var result = TrendsReportBuilder.BuildPeriod(Input(periodEnd.AddDays(-30), periodEnd, risks: risks));

        Assert.NotNull(result.AverageOutstandingRiskAgeHours);
        Assert.Equal(20.0, result.AverageOutstandingRiskAgeHours!.Value, 1);
        Assert.Equal(2, result.OutstandingRiskRows.Count);
    }

    [Fact]
    public void RestoreTestsPerformed_CountsRawTimestampsInPeriod()
    {
        var result = TrendsReportBuilder.BuildPeriod(Input(
            DateTime.UtcNow.AddDays(-30), DateTime.UtcNow,
            restoreTests: [DateTime.UtcNow.AddDays(-10), DateTime.UtcNow.AddDays(-20)]));

        Assert.Equal(2, result.RestoreTestsPerformed);
    }

    [Fact]
    public void Compare_ReturnsBothPeriodsIndependently()
    {
        var periodA = Input(DateTime.UtcNow.AddDays(-60), DateTime.UtcNow.AddDays(-30), snapshotRows: [SnapshotRow("A", BackupHealth.Failed)]);
        var periodB = Input(DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, snapshotRows: [SnapshotRow("A", BackupHealth.OnTrack)]);

        var comparison = TrendsReportBuilder.Compare(periodA, periodB);

        Assert.Equal(0.0, comparison.PeriodA.OnTrackPct);
        Assert.Equal(100.0, comparison.PeriodB.OnTrackPct);
    }
}
