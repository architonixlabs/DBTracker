using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class LifecycleWorkspaceBuilderTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc);

    private static LifecycleWorkspaceInput Input(
        int id = 1, string name = "INST-1", DatabaseEngine engine = DatabaseEngine.SqlServer,
        string? family = "SQL Server", VersionResolutionStatus? resolutionStatus = VersionResolutionStatus.Resolved,
        DateTime? observedAtUtc = null, SupportRunwayResult? runway = null,
        LifecycleCoverageStatus coverage = LifecycleCoverageStatus.OnApprovedBaseline,
        InstanceCriticality criticality = InstanceCriticality.Medium, string? ownerTeamName = "Platform",
        int clusterNodeCount = 0, CampaignState? latestCampaignState = null, int expiringExceptionCount = 0) =>
        new(id, name, engine, family, resolutionStatus, observedAtUtc ?? Now.AddDays(-10), runway, coverage, criticality, ownerTeamName, clusterNodeCount, latestCampaignState, expiringExceptionCount);

    [Fact]
    public void BuildRows_OneRowPerInput_NeverExpandedByClusterNodeCount()
    {
        var inputs = new[] { Input(clusterNodeCount: 5) };

        var rows = LifecycleWorkspaceBuilder.BuildRows(inputs, Now);

        Assert.Single(rows);
        Assert.Equal(5, rows[0].ClusterNodeCount);
    }

    [Fact]
    public void UnresolvedIdentity_IsFlaggedUnknownOrStale()
    {
        var rows = LifecycleWorkspaceBuilder.BuildRows([Input(resolutionStatus: VersionResolutionStatus.Unresolved)], Now);

        Assert.True(rows[0].IsIdentityUnknownOrStale);
    }

    [Fact]
    public void NullIdentityResolutionStatus_IsFlaggedUnknownOrStale()
    {
        var rows = LifecycleWorkspaceBuilder.BuildRows([Input(resolutionStatus: null)], Now);

        Assert.True(rows[0].IsIdentityUnknownOrStale);
    }

    [Fact]
    public void ObservedLongAgo_IsFlaggedStale_EvenIfResolved()
    {
        var rows = LifecycleWorkspaceBuilder.BuildRows([Input(observedAtUtc: Now.AddDays(-400))], Now, staleIdentityDays: 365);

        Assert.True(rows[0].IsIdentityUnknownOrStale);
    }

    [Fact]
    public void RecentlyObserved_ResolvedIdentity_IsNotFlagged()
    {
        var rows = LifecycleWorkspaceBuilder.BuildRows([Input(observedAtUtc: Now.AddDays(-10))], Now, staleIdentityDays: 365);

        Assert.False(rows[0].IsIdentityUnknownOrStale);
    }

    [Fact]
    public void NullRunway_BecomesUnknownSupportPhase_NotSilentlyOmitted()
    {
        var rows = LifecycleWorkspaceBuilder.BuildRows([Input(runway: null)], Now);

        Assert.True(rows[0].SupportPhaseIsUnknown);
        Assert.Equal(SupportPhase.Unknown, rows[0].SupportPhase);
    }

    [Fact]
    public void BlockedCampaignState_SetsIsBlockedOrLate()
    {
        var rows = LifecycleWorkspaceBuilder.BuildRows([Input(latestCampaignState: CampaignState.Blocked)], Now);

        Assert.True(rows[0].IsBlockedOrLate);
    }

    [Fact]
    public void ScheduledCampaignState_IsNotBlockedOrLate()
    {
        var rows = LifecycleWorkspaceBuilder.BuildRows([Input(latestCampaignState: CampaignState.Scheduled)], Now);

        Assert.False(rows[0].IsBlockedOrLate);
    }

    [Fact]
    public void ApplyFilter_Engine_NarrowsCorrectly()
    {
        var rows = LifecycleWorkspaceBuilder.BuildRows([Input(id: 1, engine: DatabaseEngine.SqlServer), Input(id: 2, engine: DatabaseEngine.PostgreSql)], Now);

        var filtered = LifecycleWorkspaceBuilder.ApplyFilter(rows, new LifecycleWorkspaceFilter(Engine: DatabaseEngine.PostgreSql));

        Assert.Single(filtered);
        Assert.Equal(DatabaseEngine.PostgreSql, filtered[0].Engine);
    }

    [Fact]
    public void ApplyFilter_Criticality_NarrowsCorrectly()
    {
        var rows = LifecycleWorkspaceBuilder.BuildRows([Input(id: 1, criticality: InstanceCriticality.Critical), Input(id: 2, criticality: InstanceCriticality.Low)], Now);

        var filtered = LifecycleWorkspaceBuilder.ApplyFilter(rows, new LifecycleWorkspaceFilter(Criticality: InstanceCriticality.Critical));

        Assert.Single(filtered);
    }

    [Fact]
    public void ApplyFilter_UnknownOrStaleIdentityOnly()
    {
        var rows = LifecycleWorkspaceBuilder.BuildRows(
            [Input(id: 1, resolutionStatus: VersionResolutionStatus.Unresolved), Input(id: 2, resolutionStatus: VersionResolutionStatus.Resolved)], Now);

        var filtered = LifecycleWorkspaceBuilder.ApplyFilter(rows, new LifecycleWorkspaceFilter(UnknownOrStaleIdentityOnly: true));

        Assert.Single(filtered);
        Assert.Equal(1, filtered[0].InstanceId);
    }

    [Fact]
    public void ApplyFilter_DeadlineWithinDays_ExcludesUnknownSupportPhase()
    {
        var withDeadline = Input(id: 1, runway: new SupportRunwayResult(SupportPhase.Mainstream, "label", Now.AddDays(10), 10, false, ""));
        var unknown = Input(id: 2, runway: SupportRunwayResult.Unknown("no data"));

        var rows = LifecycleWorkspaceBuilder.BuildRows([withDeadline, unknown], Now);
        var filtered = LifecycleWorkspaceBuilder.ApplyFilter(rows, new LifecycleWorkspaceFilter(DeadlineWithinDays: 30));

        Assert.Single(filtered);
        Assert.Equal(1, filtered[0].InstanceId);
    }

    [Fact]
    public void ApplyFilter_DeadlineWithinDays_IncludesOverdueNegativeDays()
    {
        var overdue = Input(runway: new SupportRunwayResult(SupportPhase.EndOfLife, "label", Now.AddDays(-30), -30, false, ""));

        var rows = LifecycleWorkspaceBuilder.BuildRows([overdue], Now);
        var filtered = LifecycleWorkspaceBuilder.ApplyFilter(rows, new LifecycleWorkspaceFilter(DeadlineWithinDays: 0));

        Assert.Single(filtered);
    }

    [Fact]
    public void Summarize_ReportsDenominatorMatchingAndUnknownCounts_Independently()
    {
        var rows = LifecycleWorkspaceBuilder.BuildRows(
            [Input(id: 1, resolutionStatus: VersionResolutionStatus.Unresolved, runway: null),
             Input(id: 2, resolutionStatus: VersionResolutionStatus.Resolved, runway: new SupportRunwayResult(SupportPhase.Mainstream, "l", Now.AddDays(5), 5, false, ""))], Now);

        var matching = LifecycleWorkspaceBuilder.ApplyFilter(rows, new LifecycleWorkspaceFilter(Engine: DatabaseEngine.SqlServer));
        var summary = LifecycleWorkspaceBuilder.Summarize(rows, matching);

        Assert.Equal(2, summary.Denominator);
        Assert.Equal(2, summary.MatchingCount);
        Assert.Equal(1, summary.UnknownIdentityCount);
        Assert.Equal(1, summary.UnknownSupportPhaseCount);
    }

    [Fact]
    public void Summarize_OldestObservedAtUtc_IgnoresNulls_ReturnsNullWhenAllNull()
    {
        var input = new LifecycleWorkspaceInput(1, "X", DatabaseEngine.MySql, null, null, null, null, LifecycleCoverageStatus.Unknown, InstanceCriticality.Unknown, null, 0, null, 0);
        var rows = LifecycleWorkspaceBuilder.BuildRows([input], Now);

        var summary = LifecycleWorkspaceBuilder.Summarize(rows, rows);

        Assert.Null(summary.OldestObservedAtUtc);
    }
}
