namespace DBTracker.Core.Services;

/// <summary>Dedupe for threshold-based support-runway notices: a notice fires again only when a more urgent threshold is crossed. Overdue (days &lt;= 0) is always more urgent, so the due-now notice still fires after the 30-day one.</summary>
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
