using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>Raw per-instance facts the D15 lifecycle workspace is built from — one row per DatabaseInstance, never per cluster node (ClusterNodeCount is carried as metadata on the row instead of expanding into separate rows).</summary>
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
    int ExpiringExceptionCount);

/// <summary>One computed workspace row — adds the derived unknown/stale-identity and blocked/late flags on top of the raw input facts.</summary>
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
    DateTime? IdentityObservedAtUtc);

/// <summary>Every filter dimension D15 asks for. A null/false field means "don't filter on this".</summary>
public record LifecycleWorkspaceFilter(
    DatabaseEngine? Engine = null,
    string? FamilyContains = null,
    SupportPhase? SupportPhase = null,
    int? DeadlineWithinDays = null,
    LifecycleCoverageStatus? Coverage = null,
    bool UnknownOrStaleIdentityOnly = false,
    InstanceCriticality? Criticality = null,
    string? OwnerTeamNameContains = null,
    CampaignState? CampaignState = null);

/// <summary>Denominator/unknown-count facts shown alongside every D15 view and export — never hidden behind a filtered count alone.</summary>
public record LifecycleWorkspaceSummary(
    int Denominator,
    int MatchingCount,
    int UnknownIdentityCount,
    int UnknownSupportPhaseCount,
    DateTime? OldestObservedAtUtc);

/// <summary>
/// Pure (no I/O) projection/filter/summary engine for D15's lifecycle workspace — the same shape as
/// InstanceListRow/InstanceListFilter from U06, applied to lifecycle facts instead of backup health.
/// BuildRows never expands a cluster into one row per node; ClusterNodeCount stays metadata on the
/// single instance row so counts shown on the workspace/reports never double-count nodes.
/// </summary>
public static class LifecycleWorkspaceBuilder
{
    public const int DefaultStaleIdentityDays = 365;

    public static IReadOnlyList<LifecycleWorkspaceRow> BuildRows(IReadOnlyList<LifecycleWorkspaceInput> inputs, DateTime nowUtc, int staleIdentityDays = DefaultStaleIdentityDays) =>
        inputs.Select(i => ToRow(i, nowUtc, staleIdentityDays)).ToList();

    private static LifecycleWorkspaceRow ToRow(LifecycleWorkspaceInput i, DateTime nowUtc, int staleIdentityDays)
    {
        var identityUnresolved = i.IdentityResolutionStatus is null or not VersionResolutionStatus.Resolved;
        var identityStale = i.IdentityObservedAtUtc is null || (nowUtc - i.IdentityObservedAtUtc.Value).TotalDays > staleIdentityDays;
        var runway = i.Runway ?? Services.SupportRunwayResult.Unknown("No support-runway data available for this instance.");

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
            IsBlockedOrLate: i.LatestCampaignState == CampaignState.Blocked,
            i.IdentityObservedAtUtc);
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
            query = query.Where(r => r.IdentityFamily is not null && r.IdentityFamily.Contains(filter.FamilyContains, StringComparison.OrdinalIgnoreCase));
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
        if (!string.IsNullOrWhiteSpace(filter.OwnerTeamNameContains))
        {
            query = query.Where(r => r.OwnerTeamName is not null && r.OwnerTeamName.Contains(filter.OwnerTeamNameContains, StringComparison.OrdinalIgnoreCase));
        }
        if (filter.CampaignState is { } state)
        {
            query = query.Where(r => r.LatestCampaignState == state);
        }

        return query.OrderBy(r => r.InstanceName).ToList();
    }

    public static LifecycleWorkspaceSummary Summarize(IReadOnlyList<LifecycleWorkspaceRow> allRows, IReadOnlyList<LifecycleWorkspaceRow> matchingRows) =>
        new(
            Denominator: allRows.Count,
            MatchingCount: matchingRows.Count,
            UnknownIdentityCount: allRows.Count(r => r.IsIdentityUnknownOrStale),
            UnknownSupportPhaseCount: allRows.Count(r => r.SupportPhaseIsUnknown),
            OldestObservedAtUtc: allRows.Where(r => r.IdentityObservedAtUtc is not null).Select(r => r.IdentityObservedAtUtc).DefaultIfEmpty(null).Min());
}
