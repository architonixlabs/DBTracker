using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

public record CapacityForecastResult(
    bool HasSufficientData,
    double? ProjectedGrowthPerDayGb,
    DateTime? EarliestPlausibleExhaustionUtc,
    DateTime? LatestPlausibleExhaustionUtc,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<string> MissingDataGaps);

/// <summary>Capacity-exhaustion forecast from imported growth measurements. Gives an earliest/latest range from the fastest and slowest observed growth, with assumptions and gaps, never one precise date.</summary>
public static class CapacityForecastBuilder
{
    private static readonly string[] StandardAssumptions =
    [
        "Assumes linear growth continues at the historically observed rate.",
        "Does not account for seasonal spikes, data archiving/purges, or storage/capacity expansion.",
        "Expressed as an earliest/latest plausible range, not a single promised exhaustion date."
    ];

    public static CapacityForecastResult Build(IReadOnlyCollection<CapacityMeasurement> measurements, double? capacityLimitGb, DateTime nowUtc)
    {
        var assumptions = StandardAssumptions.ToList();
        var gaps = new List<string>();

        var growth = measurements
            .Where(m => m.MeasurementType == CapacityMeasurementType.Growth)
            .OrderBy(m => m.MeasuredAtUtc)
            .ToList();

        if (growth.Count < 2)
        {
            gaps.Add("Need at least two timestamped growth measurements to project a trend.");
        }
        if (capacityLimitGb is null)
        {
            gaps.Add("No storage capacity limit recorded. A growth rate can still be shown, but not an exhaustion date.");
        }

        if (growth.Count < 2)
        {
            return new CapacityForecastResult(false, null, null, null, assumptions, gaps);
        }

        var rates = new List<double>();
        for (var i = 1; i < growth.Count; i++)
        {
            var days = (growth[i].MeasuredAtUtc - growth[i - 1].MeasuredAtUtc).TotalDays;
            if (days > 0)
            {
                rates.Add((growth[i].Value - growth[i - 1].Value) / days);
            }
        }

        if (rates.Count == 0)
        {
            gaps.Add("Measurements don't span enough distinct time to compute a growth rate.");
            return new CapacityForecastResult(false, null, null, null, assumptions, gaps);
        }

        var avgRate = rates.Average();
        var minRate = rates.Min();
        var maxRate = rates.Max();

        if (capacityLimitGb is null)
        {
            return new CapacityForecastResult(false, avgRate, null, null, assumptions, gaps);
        }

        var latest = growth[^1];
        var remainingGb = capacityLimitGb.Value - latest.Value;

        if (remainingGb <= 0)
        {
            gaps.Add("Latest measurement is already at or beyond the recorded capacity limit.");
            return new CapacityForecastResult(true, avgRate, nowUtc, nowUtc, assumptions, gaps);
        }

        if (maxRate <= 0)
        {
            gaps.Add("Observed growth rate is flat or negative across every interval, so no exhaustion is projected from the current trend.");
            return new CapacityForecastResult(true, avgRate, null, null, assumptions, gaps);
        }

        // The fastest observed rate reaches the limit soonest (earliest plausible date); the
        // slowest positive rate reaches it latest.
        var earliest = latest.MeasuredAtUtc.AddDays(remainingGb / maxRate);
        DateTime? latestExhaustion = null;
        if (minRate > 0)
        {
            latestExhaustion = latest.MeasuredAtUtc.AddDays(remainingGb / minRate);
        }
        else
        {
            gaps.Add("The slowest observed interval shows flat or negative growth, so it gives no upper-bound exhaustion date on its own.");
        }

        return new CapacityForecastResult(true, avgRate, earliest, latestExhaustion, assumptions, gaps);
    }
}
