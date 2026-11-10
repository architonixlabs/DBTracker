using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class RestoreTestingReportBuilderTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc);

    private static RestoreEvent Event(DateTime whenUtc, RestoreOutcome outcome) => new() { TimestampUtc = whenUtc, Outcome = outcome };

    [Fact]
    public void NoRestoreEvents_ReportsNotTested()
    {
        var input = new RestoreTestingInput("NEVER-TESTED", "Acme", "Ops", InstanceRole.Active, EnvironmentTier.Production, []);

        var row = Assert.Single(RestoreTestingReportBuilder.Build([input]));

        Assert.False(row.RestoreTested);
        Assert.Null(row.LastSuccessfulRestoreUtc);
        Assert.Equal(0, row.TotalAttempts);
    }

    [Fact]
    public void OnlyFailedAttempts_StillReportsNotTested()
    {
        var input = new RestoreTestingInput("ONLY-FAILED", "Acme", "Ops", InstanceRole.Active, EnvironmentTier.Production,
            [Event(Now.AddDays(-1), RestoreOutcome.Failed)]);

        var row = Assert.Single(RestoreTestingReportBuilder.Build([input]));

        Assert.False(row.RestoreTested);
        Assert.Equal(1, row.TotalAttempts);
    }

    [Fact]
    public void SuccessfulRestore_ReportsTested_WithMostRecentSuccessDate()
    {
        var input = new RestoreTestingInput("TESTED", "Acme", "Ops", InstanceRole.Active, EnvironmentTier.Production,
        [
            Event(Now.AddDays(-30), RestoreOutcome.Success),
            Event(Now.AddDays(-2), RestoreOutcome.Success),
            Event(Now.AddDays(-1), RestoreOutcome.Failed),
        ]);

        var row = Assert.Single(RestoreTestingReportBuilder.Build([input]));

        Assert.True(row.RestoreTested);
        Assert.Equal(Now.AddDays(-2), row.LastSuccessfulRestoreUtc);
        Assert.Equal(3, row.TotalAttempts);
    }

    [Fact]
    public void UntestedInstancesSortFirst()
    {
        var tested = new RestoreTestingInput("TESTED", "Acme", "Ops", InstanceRole.Active, EnvironmentTier.Production,
            [Event(Now, RestoreOutcome.Success)]);
        var untested = new RestoreTestingInput("UNTESTED", "Acme", "Ops", InstanceRole.Active, EnvironmentTier.Production, []);

        var rows = RestoreTestingReportBuilder.Build([tested, untested]);

        Assert.Equal("UNTESTED", rows[0].InstanceName);
        Assert.Equal("TESTED", rows[1].InstanceName);
    }

    [Fact]
    public void RecentSuccess_WithinInterval_IsCurrent()
    {
        var input = new RestoreTestingInput("FRESH", "Acme", "Ops", InstanceRole.Active, EnvironmentTier.Production,
            [Event(Now.AddDays(-10), RestoreOutcome.Success)]);

        var row = Assert.Single(RestoreTestingReportBuilder.Build([input], defaultIntervalDays: 90, nowUtc: Now));

        Assert.Equal(RestoreTestRecency.Current, row.Recency);
        Assert.Equal(Now.AddDays(-10).AddDays(90), row.NextDueUtc);
    }

    [Fact]
    public void OldSuccess_BeyondInterval_IsOverdue_DistinctFromNeverTested()
    {
        var input = new RestoreTestingInput("EXPIRED", "Acme", "Ops", InstanceRole.Active, EnvironmentTier.Production,
            [Event(Now.AddDays(-200), RestoreOutcome.Success)]);

        var row = Assert.Single(RestoreTestingReportBuilder.Build([input], defaultIntervalDays: 90, nowUtc: Now));

        Assert.True(row.RestoreTested);
        Assert.Equal(RestoreTestRecency.Overdue, row.Recency);
    }

    [Fact]
    public void NeverTested_RecencyIsNeverTested_NotOverdue()
    {
        var input = new RestoreTestingInput("NEVER", "Acme", "Ops", InstanceRole.Active, EnvironmentTier.Production, []);

        var row = Assert.Single(RestoreTestingReportBuilder.Build([input], defaultIntervalDays: 90, nowUtc: Now));

        Assert.Equal(RestoreTestRecency.NeverTested, row.Recency);
        Assert.Null(row.NextDueUtc);
    }

    [Fact]
    public void PerInstanceIntervalOverride_TakesPrecedenceOverDefault()
    {
        // 30 days old would be Current under the 90-day org default, but Overdue under a 14-day override.
        var input = new RestoreTestingInput("OVERRIDE", "Acme", "Ops", InstanceRole.Active, EnvironmentTier.Production,
            [Event(Now.AddDays(-30), RestoreOutcome.Success)], IntervalDaysOverride: 14);

        var row = Assert.Single(RestoreTestingReportBuilder.Build([input], defaultIntervalDays: 90, nowUtc: Now));

        Assert.Equal(RestoreTestRecency.Overdue, row.Recency);
    }

    [Fact]
    public void OverdueCoverageSortsBeforeCurrentCoverage_AmongTestedInstances()
    {
        var current = new RestoreTestingInput("CURRENT", "Acme", "Ops", InstanceRole.Active, EnvironmentTier.Production,
            [Event(Now.AddDays(-5), RestoreOutcome.Success)]);
        var overdue = new RestoreTestingInput("OVERDUE", "Acme", "Ops", InstanceRole.Active, EnvironmentTier.Production,
            [Event(Now.AddDays(-200), RestoreOutcome.Success)]);

        var rows = RestoreTestingReportBuilder.Build([current, overdue], defaultIntervalDays: 90, nowUtc: Now);

        Assert.Equal("OVERDUE", rows[0].InstanceName);
        Assert.Equal("CURRENT", rows[1].InstanceName);
    }
}
