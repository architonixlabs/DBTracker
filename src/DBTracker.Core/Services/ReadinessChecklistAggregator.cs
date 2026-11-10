using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>Aggregated D09 checklist summary — blocking findings and unknown checks are both exposed in full, not just counted, per "summarize without hiding the underlying detail".</summary>
public record ReadinessChecklistSummary(
    int TotalCount,
    int PassedCount,
    int FailedCount,
    int UnknownCount,
    int NotApplicableCount,
    bool HasBlockingFindings,
    IReadOnlyList<ReadinessCheckItem> BlockingFindings,
    IReadOnlyList<ReadinessCheckItem> UnknownChecks);

/// <summary>
/// Pure aggregation/blocking-summary logic for a D09 readiness checklist. EffectiveStatus is the
/// single place that applies the two ways a recorded result stops counting — explicit invalidation
/// (the checklist's target changed) and evidence expiry — so a Passed/Failed item past either of
/// those is reported as Unknown here even though its stored Status field is untouched. A check that
/// was simply never performed is Unknown by its own default and is never promoted to Passed anywhere
/// in this aggregation.
/// </summary>
public static class ReadinessChecklistAggregator
{
    public static ReadinessCheckStatus EffectiveStatus(ReadinessCheckItem item, DateTime nowUtc)
    {
        if (item.IsInvalidated)
        {
            return ReadinessCheckStatus.Unknown;
        }

        if (item.EvidenceExpiresAtUtc is { } expires && expires <= nowUtc)
        {
            return ReadinessCheckStatus.Unknown;
        }

        return item.Status;
    }

    public static ReadinessChecklistSummary Summarize(IReadOnlyList<ReadinessCheckItem> items, DateTime nowUtc)
    {
        var effective = items.Select(i => (Item: i, Status: EffectiveStatus(i, nowUtc))).ToList();

        var blocking = effective.Where(e => e.Status == ReadinessCheckStatus.Failed).Select(e => e.Item).ToList();
        var unknown = effective.Where(e => e.Status == ReadinessCheckStatus.Unknown).Select(e => e.Item).ToList();

        return new ReadinessChecklistSummary(
            items.Count,
            effective.Count(e => e.Status == ReadinessCheckStatus.Passed),
            effective.Count(e => e.Status == ReadinessCheckStatus.Failed),
            unknown.Count,
            effective.Count(e => e.Status == ReadinessCheckStatus.NotApplicable),
            blocking.Count > 0,
            blocking,
            unknown);
    }
}
