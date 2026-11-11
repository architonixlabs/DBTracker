using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>Lifecycle planning report built from the same rows the workspace shows, so an export can't drift from the screen. Runway, baseline coverage, expiring exceptions and blocked migrations stay separate facts; backup health is deliberately absent.</summary>
public record LifecycleReport(
    int Denominator,
    int MatchingCount,
    int UnknownIdentityCount,
    int UnknownSupportPhaseCount,
    DateTime? OldestObservedAtUtc,
    IReadOnlyList<LifecycleWorkspaceRow> Rows,
    IReadOnlyDictionary<SupportPhase, int> SupportPhaseCounts,
    IReadOnlyDictionary<LifecycleCoverageStatus, int> CoverageCounts,
    IReadOnlyList<LifecycleWorkspaceRow> ExpiringExceptionRows,
    IReadOnlyList<LifecycleWorkspaceRow> BlockedOrLateRows);

public static class LifecycleReportBuilder
{
    public static LifecycleReport Build(IReadOnlyList<LifecycleWorkspaceRow> allRows, IReadOnlyList<LifecycleWorkspaceRow> matchingRows)
    {
        var summary = LifecycleWorkspaceBuilder.Summarize(allRows, matchingRows);

        var supportPhaseCounts = matchingRows
            .GroupBy(r => r.SupportPhase)
            .ToDictionary(g => g.Key, g => g.Count());

        var coverageCounts = matchingRows
            .GroupBy(r => r.Coverage)
            .ToDictionary(g => g.Key, g => g.Count());

        return new LifecycleReport(
            summary.Denominator,
            summary.MatchingCount,
            summary.UnknownIdentityCount,
            summary.UnknownSupportPhaseCount,
            summary.OldestObservedAtUtc,
            matchingRows,
            supportPhaseCounts,
            coverageCounts,
            ExpiringExceptionRows: matchingRows.Where(r => r.ExpiringExceptionCount > 0).OrderByDescending(r => r.ExpiringExceptionCount).ToList(),
            BlockedOrLateRows: matchingRows.Where(r => r.IsBlockedOrLate).ToList());
    }
}
