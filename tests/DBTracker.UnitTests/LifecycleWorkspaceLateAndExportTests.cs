using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class LifecycleWorkspaceLateAndExportTests
{
    private static readonly DateTime Now = new(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc);

    private static readonly SupportRunwayResult Mainstream = new(SupportPhase.Mainstream, "End of mainstream", Now.AddDays(200), 200, false, "");
    private static readonly SupportRunwayResult Ended = new(SupportPhase.EndOfLife, "End of support", Now.AddDays(-10), -10, false, "");

    private static LifecycleWorkspaceInput Input(int id = 1, string name = "INST-1", string? family = "PostgreSQL", int? major = 13,
        CampaignState? state = null, int overdueWindows = 0, SupportRunwayResult? runway = null, string? owner = "Platform",
        int deployments = 0, int hosted = 0) =>
        new(id, name, DatabaseEngine.PostgreSql, family, VersionResolutionStatus.Resolved, Now.AddDays(-5), runway ?? Mainstream,
            LifecycleCoverageStatus.OnApprovedBaseline, InstanceCriticality.High, owner, 0, state, 0,
            IdentityMajor: major, DeploymentCount: deployments, HostedDatabaseCount: hosted, OverdueChangeWindowCount: overdueWindows);

    [Fact]
    public void OpenCampaign_WithOverdueWindow_IsLate()
    {
        var row = LifecycleWorkspaceBuilder.BuildRows([Input(state: CampaignState.Scheduled, overdueWindows: 1)], Now)[0];

        Assert.True(row.IsLate);
        Assert.True(row.IsBlockedOrLate);
        Assert.False(row.IsBlocked);
        Assert.Contains("planned end has passed", row.LateReason);
    }

    [Fact]
    public void OpenCampaign_AfterSupportEnded_IsLate()
    {
        var row = LifecycleWorkspaceBuilder.BuildRows([Input(state: CampaignState.Assessing, runway: Ended)], Now)[0];

        Assert.True(row.IsLate);
        Assert.Contains("Support has ended", row.LateReason);
    }

    [Fact]
    public void CompletedCampaign_IsNeverLate_EvenWithOverdueWindows()
    {
        var row = LifecycleWorkspaceBuilder.BuildRows([Input(state: CampaignState.Completed, overdueWindows: 2, runway: Ended)], Now)[0];

        Assert.False(row.IsLate);
        Assert.False(row.IsBlockedOrLate);
    }

    [Fact]
    public void NoCampaign_IsNeverLate()
    {
        Assert.Null(LifecycleWorkspaceBuilder.DetectLate(null, 3, Ended));
    }

    [Fact]
    public void UnknownRunway_DoesNotMakeACampaignLate()
    {
        Assert.Null(LifecycleWorkspaceBuilder.DetectLate(CampaignState.InProgress, 0, SupportRunwayResult.Unknown("no data")));
    }

    [Fact]
    public void BranchFilter_MatchesFamilyAndMajor()
    {
        var rows = LifecycleWorkspaceBuilder.BuildRows([Input(id: 1, major: 13), Input(id: 2, name: "B", major: 16)], Now);

        var filtered = LifecycleWorkspaceBuilder.ApplyFilter(rows, new LifecycleWorkspaceFilter(FamilyContains: "PostgreSQL 13"));

        Assert.Single(filtered);
        Assert.Equal("PostgreSQL 13", filtered[0].Branch);
    }

    [Fact]
    public void OwnerFilter_AndUnassignedOwner_NarrowCorrectly()
    {
        var rows = LifecycleWorkspaceBuilder.BuildRows([Input(id: 1, name: "A", owner: "Platform"), Input(id: 2, name: "B", owner: null)], Now);

        Assert.Single(LifecycleWorkspaceBuilder.ApplyFilter(rows, new LifecycleWorkspaceFilter(OwnerTeamNameContains: "platform")));
        var unassigned = LifecycleWorkspaceBuilder.ApplyFilter(rows, new LifecycleWorkspaceFilter(UnassignedOwnerOnly: true));
        Assert.Single(unassigned);
        Assert.Equal("B", unassigned[0].InstanceName);
    }

    [Fact]
    public void BlockedOrLateOnly_KeepsBlockedAndLateRows()
    {
        var rows = LifecycleWorkspaceBuilder.BuildRows(
            [Input(id: 1, name: "A", state: CampaignState.Blocked), Input(id: 2, name: "B", state: CampaignState.Scheduled, overdueWindows: 1), Input(id: 3, name: "C", state: CampaignState.Scheduled)], Now);

        var filtered = LifecycleWorkspaceBuilder.ApplyFilter(rows, new LifecycleWorkspaceFilter(BlockedOrLateOnly: true));

        Assert.Equal(["A", "B"], filtered.Select(r => r.InstanceName));
    }

    [Fact]
    public void Summary_CountsHostedDatabasesAndDeploymentsOfMatchingRowsOnce()
    {
        var rows = LifecycleWorkspaceBuilder.BuildRows([Input(id: 1, name: "A", hosted: 4, deployments: 2), Input(id: 2, name: "B", hosted: 1)], Now);
        var matching = LifecycleWorkspaceBuilder.ApplyFilter(rows, new LifecycleWorkspaceFilter(FamilyContains: "PostgreSQL"));

        var summary = LifecycleWorkspaceBuilder.Summarize(rows, matching);

        Assert.Equal(5, summary.MatchingHostedDatabaseCount);
        Assert.Equal(2, summary.MatchingDeploymentCount);
    }

    [Fact]
    public void Csv_CarriesDenominatorFilterAndFreshness()
    {
        var rows = LifecycleWorkspaceBuilder.BuildRows([Input(id: 1, name: "A"), Input(id: 2, name: "B", family: null, major: null)], Now);
        var matching = rows.Take(1).ToList();
        var summary = LifecycleWorkspaceBuilder.Summarize(rows, matching);

        var csv = LifecycleWorkspaceCsvWriter.Build(summary, matching, "Engine=PostgreSql", Now);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Contains(lines, l => l.StartsWith("# Rows in this file: 1 of 2"));
        Assert.Contains(lines, l => l == "# Filter: Engine=PostgreSql");
        Assert.Contains(lines, l => l.StartsWith("# Oldest identity observation: "));
        var header = lines.First(l => !l.StartsWith('#'));
        Assert.Contains("IdentityObservedAtUtc", header);
        Assert.Contains("HostedDatabases", header);
        Assert.Equal(2, lines.Count(l => !l.StartsWith('#')));
    }

    [Fact]
    public void Csv_NeutralisesFormulaCells()
    {
        Assert.Equal("'=cmd", LifecycleWorkspaceCsvWriter.Csv("=cmd"));
    }
}
