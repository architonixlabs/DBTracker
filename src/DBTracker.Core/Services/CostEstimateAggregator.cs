using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

public record CostEstimateSummary(
    int Count,
    decimal? TotalExtendedSupportCost,
    decimal? TotalMigrationCost,
    IReadOnlyList<string> CurrenciesUsed,
    bool HasMixedCurrencies,
    DateTime? LatestEstimateDateUtc,
    IReadOnlyList<EditionCostEstimate> MissingReviewer);

/// <summary>
/// D21 — pure aggregation of an instance's reviewed cost estimates: totals by type, currency-mix
/// flagging (summing mismatched currencies would be meaningless, so this only ever sums within one
/// currency and flags the mix rather than silently combining figures), and which estimates still lack
/// a reviewer. Never masks anything itself — CostEvidenceMasker is the separate, role-aware step
/// callers apply to whatever this returns before it reaches a non-authorized viewer.
/// </summary>
public static class CostEstimateAggregator
{
    public static CostEstimateSummary Summarize(IReadOnlyCollection<EditionCostEstimate> estimates)
    {
        if (estimates.Count == 0)
        {
            return new CostEstimateSummary(0, null, null, [], false, null, []);
        }

        var currencies = estimates.Select(e => e.Currency).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var hasMixedCurrencies = currencies.Count > 1;

        decimal? SumByType(CostEstimateType type)
        {
            var matching = estimates.Where(e => e.EstimateType == type).ToList();
            if (matching.Count == 0)
            {
                return null;
            }
            // Only sum when every matching estimate shares one currency — a mixed-currency set
            // reports its total as unavailable rather than adding incompatible figures together.
            var distinctCurrencies = matching.Select(e => e.Currency).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            return distinctCurrencies == 1 ? matching.Sum(e => e.EstimatedAmount) : null;
        }

        return new CostEstimateSummary(
            estimates.Count,
            SumByType(CostEstimateType.ExtendedSupport),
            SumByType(CostEstimateType.Migration),
            currencies,
            hasMixedCurrencies,
            estimates.Max(e => e.EstimateDateUtc),
            estimates.Where(e => string.IsNullOrWhiteSpace(e.ReviewedBy)).ToList());
    }
}
