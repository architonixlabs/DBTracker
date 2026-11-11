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

/// <summary>Totals an instance's cost estimates by type, flags mixed currencies instead of summing them, and lists unreviewed estimates. Masking is done separately by CostEvidenceMasker.</summary>
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
            // Mixed currencies report the total as unavailable rather than adding incompatible figures.
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
