namespace DBTracker.Core.Services;

/// <summary>
/// Pure dedupe/escalation rule for D03's configurable 365/180/90/30-day support-runway notices.
/// "Crossed" means days-remaining has dropped to at or below a configured threshold; the most
/// urgent (smallest) threshold crossed is what matters. A notice only fires again once a *more*
/// urgent threshold than the last one notified has been crossed, so a sweep doesn't re-notify every
/// run — but overdue (days &lt;= 0) always counts as more urgent than any positive threshold, so the
/// "it's now actually due" notice still fires even after the 30-day one already did.
/// </summary>
public static class SupportRunwayNoticeEvaluator
{
    /// <summary>The most urgent configured threshold (days-before-deadline) that has been crossed, or null if none has.</summary>
    public static int? ThresholdCrossed(int? daysToMilestone, IReadOnlyCollection<int> thresholds)
    {
        if (daysToMilestone is null)
        {
            return null;
        }

        var all = thresholds.Append(0).Distinct().OrderBy(t => t).ToList();
        var crossed = all.Where(t => daysToMilestone <= t).ToList();
        return crossed.Count == 0 ? null : crossed.Min();
    }

    public static bool ShouldNotify(int? daysToMilestone, IReadOnlyCollection<int> thresholds, int? lastNotifiedThresholdDays)
    {
        var crossed = ThresholdCrossed(daysToMilestone, thresholds);
        if (crossed is null)
        {
            return false;
        }

        return lastNotifiedThresholdDays is null || crossed < lastNotifiedThresholdDays;
    }

    public static IReadOnlyList<int> ParseThresholds(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            return [365, 180, 90, 30];
        }

        var parsed = configured.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s, out var n) ? n : (int?)null)
            .Where(n => n is > 0)
            .Select(n => n!.Value)
            .Distinct()
            .ToList();

        return parsed.Count == 0 ? [365, 180, 90, 30] : parsed;
    }
}
