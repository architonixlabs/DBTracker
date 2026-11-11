using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>Checklist summary listing blocking findings and unknown checks in full, not just counted.</summary>
public record ReadinessChecklistSummary(
    int TotalCount,
    int PassedCount,
    int FailedCount,
    int UnknownCount,
    int NotApplicableCount,
    bool HasBlockingFindings,
    IReadOnlyList<ReadinessCheckItem> BlockingFindings,
    IReadOnlyList<ReadinessCheckItem> UnknownChecks);

/// <summary>Checklist aggregation. EffectiveStatus is the one place invalidation and evidence expiry turn a recorded result back into Unknown; the stored Status is untouched, and nothing unperformed becomes Passed.</summary>
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
