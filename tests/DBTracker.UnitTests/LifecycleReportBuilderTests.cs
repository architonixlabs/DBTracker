using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class LifecycleReportBuilderTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc);

    private static LifecycleWorkspaceRow Row(
        int id, SupportPhase phase = SupportPhase.Mainstream, LifecycleCoverageStatus coverage = LifecycleCoverageStatus.OnApprovedBaseline,
        int expiringExceptionCount = 0, bool isBlockedOrLate = false) =>
        new(id, $"INST-{id}", DatabaseEngine.SqlServer, "SQL Server", phase, 10, phase == SupportPhase.Unknown, coverage,
            InstanceCriticality.Medium, "Platform", 0, null, expiringExceptionCount, false, isBlockedOrLate, Now);

    [Fact]
    public void Build_UsesExactlyThePassedInRowSets_NoSeparateComputation()
    {
        var all = new[] { Row(1), Row(2) };
        var matching = new[] { Row(1) };

        var report = LifecycleReportBuilder.Build(all, matching);

        Assert.Equal(2, report.Denominator);
        Assert.Equal(1, report.MatchingCount);
        Assert.Same(matching, report.Rows);
    }

    [Fact]
    public void SupportPhaseCounts_GroupsOnlyMatchingRows()
    {
        var all = new[] { Row(1, SupportPhase.Mainstream), Row(2, SupportPhase.Extended) };
        var matching = new[] { all[0] };

        var report = LifecycleReportBuilder.Build(all, matching);

        Assert.Equal(1, report.SupportPhaseCounts[SupportPhase.Mainstream]);
        Assert.False(report.SupportPhaseCounts.ContainsKey(SupportPhase.Extended));
    }

    [Fact]
    public void CoverageCounts_GroupsOnlyMatchingRows()
    {
        var all = new[] { Row(1, coverage: LifecycleCoverageStatus.BehindBaseline), Row(2, coverage: LifecycleCoverageStatus.OnApprovedBaseline) };

        var report = LifecycleReportBuilder.Build(all, all);

        Assert.Equal(1, report.CoverageCounts[LifecycleCoverageStatus.BehindBaseline]);
        Assert.Equal(1, report.CoverageCounts[LifecycleCoverageStatus.OnApprovedBaseline]);
    }

    [Fact]
    public void ExpiringExceptionRows_OnlyIncludesRowsWithAPositiveCount()
    {
        var all = new[] { Row(1, expiringExceptionCount: 2), Row(2, expiringExceptionCount: 0) };

        var report = LifecycleReportBuilder.Build(all, all);

        Assert.Single(report.ExpiringExceptionRows);
        Assert.Equal(1, report.ExpiringExceptionRows[0].InstanceId);
    }

    [Fact]
    public void BlockedOrLateRows_OnlyIncludesFlaggedRows()
    {
        var all = new[] { Row(1, isBlockedOrLate: true), Row(2, isBlockedOrLate: false) };

        var report = LifecycleReportBuilder.Build(all, all);

        Assert.Single(report.BlockedOrLateRows);
        Assert.Equal(1, report.BlockedOrLateRows[0].InstanceId);
    }

    [Fact]
    public void BackupHealthIsNeverPartOfThisReportType()
    {
        // Structural assertion: LifecycleReport has no backup-health-related member — health is
        // tracked elsewhere and must never be blended into the lifecycle report.
        var properties = typeof(LifecycleReport).GetProperties().Select(p => p.Name);

        Assert.DoesNotContain(properties, n => n.Contains("Health", StringComparison.OrdinalIgnoreCase)
            || n.Contains("Backup", StringComparison.OrdinalIgnoreCase));
    }
}
