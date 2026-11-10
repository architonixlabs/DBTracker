using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class BackupActivityReportBuilderTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc);

    private static BackupActivityRow Row(DateTime whenUtc, BackupStatus status = BackupStatus.Success, double? sizeMb = 100, double? durationMinutes = 10) =>
        new("INSTANCE", "Acme", "Ops", DatabaseEngine.PostgreSql, whenUtc, BackupType.Full, status, sizeMb, durationMinutes, BackupSource.Manual);

    [Fact]
    public void InRange_ExcludesRowsOutsideWindow()
    {
        var rows = new[]
        {
            Row(Now.AddDays(-1)),
            Row(Now.AddDays(-10)),
            Row(Now.AddDays(-40)),
        };

        var result = BackupActivityReportBuilder.InRange(rows, Now.AddDays(-14), Now);

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, r => r.TimestampUtc == Now.AddDays(-40));
    }

    [Fact]
    public void InRange_OrdersNewestFirst()
    {
        var rows = new[]
        {
            Row(Now.AddDays(-5)),
            Row(Now.AddDays(-1)),
            Row(Now.AddDays(-3)),
        };

        var result = BackupActivityReportBuilder.InRange(rows, Now.AddDays(-14), Now);

        Assert.Equal(Now.AddDays(-1), result[0].TimestampUtc);
        Assert.Equal(Now.AddDays(-3), result[1].TimestampUtc);
        Assert.Equal(Now.AddDays(-5), result[2].TimestampUtc);
    }

    [Fact]
    public void Summarize_CountsByStatus_AndSumsSize()
    {
        var rows = new[]
        {
            Row(Now, BackupStatus.Success, sizeMb: 100),
            Row(Now, BackupStatus.Success, sizeMb: 200),
            Row(Now, BackupStatus.Failed, sizeMb: null),
            Row(Now, BackupStatus.Missed, sizeMb: null),
        };

        var summary = BackupActivityReportBuilder.Summarize(rows);

        Assert.Equal(4, summary.TotalRuns);
        Assert.Equal(2, summary.SuccessCount);
        Assert.Equal(1, summary.FailedCount);
        Assert.Equal(1, summary.MissedCount);
        Assert.Equal(300, summary.TotalSizeMb);
    }

    [Fact]
    public void Summarize_AvgDuration_IgnoresNullsAndIsNullWhenNoneHaveDuration()
    {
        var withDurations = new[]
        {
            Row(Now, durationMinutes: 10),
            Row(Now, durationMinutes: 20),
            Row(Now, durationMinutes: null),
        };

        Assert.Equal(15, BackupActivityReportBuilder.Summarize(withDurations).AvgDurationMinutes);

        var noDurations = new[] { Row(Now, durationMinutes: null) };
        Assert.Null(BackupActivityReportBuilder.Summarize(noDurations).AvgDurationMinutes);
    }
}
