using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>Raw facts for one workspace row. One row per instance, never per cluster node; ClusterNodeCount, DeploymentCount and
/// HostedDatabaseCount are metadata on that row, so totals never double-count.</summary>
public record LifecycleWorkspaceInput(
    int InstanceId,
    string InstanceName,
    DatabaseEngine Engine,
    string? IdentityFamily,
    VersionResolutionStatus? IdentityResolutionStatus,
    DateTime? IdentityObservedAtUtc,
    SupportRunwayResult? Runway,
    LifecycleCoverageStatus Coverage,
    InstanceCriticality Criticality,
    string? OwnerTeamName,
    int ClusterNodeCount,
    CampaignState? LatestCampaignState,
    int ExpiringExceptionCount,
    int? IdentityMajor = null,
    int DeploymentCount = 0,
    int HostedDatabaseCount = 0,
    int OverdueChangeWindowCount = 0);

/// <summary>A computed workspace row, adding the unknown/stale-identity and blocked/late flags.</summary>
public record LifecycleWorkspaceRow(
    int InstanceId,
    string InstanceName,
    DatabaseEngine Engine,
    string? IdentityFamily,
    SupportPhase SupportPhase,
    int? DaysToNextMilestone,
    bool SupportPhaseIsUnknown,
    LifecycleCoverageStatus Coverage,
    InstanceCriticality Criticality,
    string? OwnerTeamName,
    int ClusterNodeCount,
    CampaignState? LatestCampaignState,
    int ExpiringExceptionCount,
    bool IsIdentityUnknownOrStale,
    bool IsBlockedOrLate,
    DateTime? IdentityObservedAtUtc,
    string? Branch = null,
    int DeploymentCount = 0,
    int HostedDatabaseCount = 0,
    string? LateReason = null)
{
    public bool IsBlocked => LatestCampaignState == CampaignState.Blocked;
    public bool IsLate => LateReason is not null;
}

/// <summary>Workspace filters. A null/false field means don't filter on it.</summary>
/// <param name="FamilyContains">Matches the engine family or the branch (family plus major version), e.g. "PostgreSQL 13".</param>
public record LifecycleWorkspaceFilter(
    DatabaseEngine? Engine = null,
    string? FamilyContains = null,
    SupportPhase? SupportPhase = null,
    int? DeadlineWithinDays = null,
    LifecycleCoverageStatus? Coverage = null,
    bool UnknownOrStaleIdentityOnly = false,
    InstanceCriticality? Criticality = null,
    string? OwnerTeamNameContains = null,
    CampaignState? CampaignState = null,
    bool BlockedOrLateOnly = false,
    bool UnassignedOwnerOnly = false);

/// <summary>Denominator and unknown counts shown with every view and export, never just a filtered count.</summary>
public record LifecycleWorkspaceSummary(
    int Denominator,
    int MatchingCount,
    int UnknownIdentityCount,
    int UnknownSupportPhaseCount,
    DateTime? OldestObservedAtUtc,
    int MatchingHostedDatabaseCount = 0,
    int MatchingDeploymentCount = 0,
    int MatchingLateCount = 0);

/// <summary>Projection, filter and summary for the lifecycle workspace. Clusters are never expanded per node, so counts never double up.</summary>
public static class LifecycleWorkspaceBuilder
{
    public const int DefaultStaleIdentityDays = 365;

    private static readonly CampaignState[] OpenCampaignStates =
    [
        CampaignState.Proposed, CampaignState.Assessing, CampaignState.Blocked, CampaignState.Ready,
        CampaignState.Scheduled, CampaignState.InProgress, CampaignState.Validating
    ];

    public static IReadOnlyList<LifecycleWorkspaceRow> BuildRows(IReadOnlyList<LifecycleWorkspaceInput> inputs, DateTime nowUtc, int staleIdentityDays = DefaultStaleIdentityDays) =>
        inputs.Select(i => ToRow(i, nowUtc, staleIdentityDays)).ToList();

    /// <summary>Branch label used for filtering and export: family plus major version when both are known.</summary>
    public static string? BranchLabel(string? family, int? major) =>
        string.IsNullOrWhiteSpace(family) ? null : major is { } m ? $"{family} {m}" : family;

    /// <summary>Why an open migration counts as late, or null when it doesn't: a change window ended without completing, or support ended
    /// while the migration is still open. A closed or absent campaign is never late.</summary>
    public static string? DetectLate(CampaignState? latestCampaignState, int overdueChangeWindowCount, SupportRunwayResult runway)
    {
        if (latestCampaignState is not { } state || !OpenCampaignStates.Contains(state))
        {
            return null;
        }
        if (overdueChangeWindowCount > 0)
        {
            return overdueChangeWindowCount == 1
                ? "A change window's planned end has passed without it being completed."
                : $"{overdueChangeWindowCount} change windows' planned ends have passed without them being completed.";
        }
        if (!runway.IsUnknown && (runway.Phase == SupportPhase.EndOfLife || runway.DaysToNextMilestone is < 0))
        {
            return "Support has ended while the migration is still open.";
        }
        return null;
    }

    private static LifecycleWorkspaceRow ToRow(LifecycleWorkspaceInput i, DateTime nowUtc, int staleIdentityDays)
    {
        var identityUnresolved = i.IdentityResolutionStatus is null or not VersionResolutionStatus.Resolved;
        var identityStale = i.IdentityObservedAtUtc is null || (nowUtc - i.IdentityObservedAtUtc.Value).TotalDays > staleIdentityDays;
        var runway = i.Runway ?? Services.SupportRunwayResult.Unknown("No support-runway data available for this instance.");
        var lateReason = DetectLate(i.LatestCampaignState, i.OverdueChangeWindowCount, runway);

        return new LifecycleWorkspaceRow(
            i.InstanceId,
            i.InstanceName,
            i.Engine,
            i.IdentityFamily,
            runway.Phase,
            runway.DaysToNextMilestone,
            runway.IsUnknown,
            i.Coverage,
            i.Criticality,
            i.OwnerTeamName,
            i.ClusterNodeCount,
            i.LatestCampaignState,
            i.ExpiringExceptionCount,
            IsIdentityUnknownOrStale: identityUnresolved || identityStale,
            IsBlockedOrLate: i.LatestCampaignState == CampaignState.Blocked || lateReason is not null,
            i.IdentityObservedAtUtc,
            Branch: BranchLabel(i.IdentityFamily, i.IdentityMajor),
            DeploymentCount: i.DeploymentCount,
            HostedDatabaseCount: i.HostedDatabaseCount,
            LateReason: lateReason);
    }

    public static IReadOnlyList<LifecycleWorkspaceRow> ApplyFilter(IReadOnlyList<LifecycleWorkspaceRow> rows, LifecycleWorkspaceFilter filter)
    {
        var query = rows.AsEnumerable();

        if (filter.Engine is { } engine)
        {
            query = query.Where(r => r.Engine == engine);
        }
        if (!string.IsNullOrWhiteSpace(filter.FamilyContains))
        {
            var term = filter.FamilyContains.Trim();
            query = query.Where(r =>
                (r.IdentityFamily is not null && r.IdentityFamily.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
                (r.Branch is not null && r.Branch.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }
        if (filter.SupportPhase is { } phase)
        {
            query = query.Where(r => r.SupportPhase == phase);
        }
        if (filter.DeadlineWithinDays is { } days)
        {
            query = query.Where(r => !r.SupportPhaseIsUnknown && r.DaysToNextMilestone is { } d && d <= days);
        }
        if (filter.Coverage is { } coverage)
        {
            query = query.Where(r => r.Coverage == coverage);
        }
        if (filter.UnknownOrStaleIdentityOnly)
        {
            query = query.Where(r => r.IsIdentityUnknownOrStale);
        }
        if (filter.Criticality is { } criticality)
        {
            query = query.Where(r => r.Criticality == criticality);
        }
        if (filter.UnassignedOwnerOnly)
        {
            query = query.Where(r => string.IsNullOrWhiteSpace(r.OwnerTeamName));
        }
        else if (!string.IsNullOrWhiteSpace(filter.OwnerTeamNameContains))
        {
            query = query.Where(r => r.OwnerTeamName is not null && r.OwnerTeamName.Contains(filter.OwnerTeamNameContains.Trim(), StringComparison.OrdinalIgnoreCase));
        }
        if (filter.CampaignState is { } state)
        {
            query = query.Where(r => r.LatestCampaignState == state);
        }
        if (filter.BlockedOrLateOnly)
        {
            query = query.Where(r => r.IsBlockedOrLate);
        }

        return query.OrderBy(r => r.InstanceName).ToList();
    }

    public static LifecycleWorkspaceSummary Summarize(IReadOnlyList<LifecycleWorkspaceRow> allRows, IReadOnlyList<LifecycleWorkspaceRow> matchingRows) =>
        new(
            Denominator: allRows.Count,
            MatchingCount: matchingRows.Count,
            UnknownIdentityCount: allRows.Count(r => r.IsIdentityUnknownOrStale),
            UnknownSupportPhaseCount: allRows.Count(r => r.SupportPhaseIsUnknown),
            OldestObservedAtUtc: allRows.Where(r => r.IdentityObservedAtUtc is not null).Select(r => r.IdentityObservedAtUtc).DefaultIfEmpty(null).Min(),
            MatchingHostedDatabaseCount: matchingRows.Sum(r => r.HostedDatabaseCount),
            MatchingDeploymentCount: matchingRows.Sum(r => r.DeploymentCount),
            MatchingLateCount: matchingRows.Count(r => r.IsLate));
}
