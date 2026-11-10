using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>
/// D15's planning report — built entirely from the SAME filtered row set the workspace shows
/// (callers pass the identical <c>matchingRows</c> they're rendering), so an export can never drift
/// from what the viewer is looking at. Sections keep support runway, patch-baseline coverage,
/// expiring exceptions and blocked/late migrations as separate, explainable facts rather than one
/// opaque score, and backup health is deliberately absent from this type entirely — it is tracked
/// elsewhere and never blended in here.
/// </summary>
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
