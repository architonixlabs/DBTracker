using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class CostEstimateAggregatorTests
{
    private static EditionCostEstimate Estimate(CostEstimateType type, decimal amount, string currency = "USD", string reviewedBy = "Marcus Chen") => new()
    {
        EstimateType = type,
        EstimatedAmount = amount,
        Currency = currency,
        EstimateDateUtc = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
        ReviewedBy = reviewedBy
    };

    [Fact]
    public void NoEstimates_ReturnsEmptySummary()
    {
        var summary = CostEstimateAggregator.Summarize([]);

        Assert.Equal(0, summary.Count);
        Assert.Null(summary.TotalExtendedSupportCost);
        Assert.Null(summary.TotalMigrationCost);
        Assert.Empty(summary.CurrenciesUsed);
    }

    [Fact]
    public void SingleCurrency_SumsByType()
    {
        var estimates = new[]
        {
            Estimate(CostEstimateType.ExtendedSupport, 10_000m),
            Estimate(CostEstimateType.ExtendedSupport, 5_000m),
            Estimate(CostEstimateType.Migration, 50_000m)
        };

        var summary = CostEstimateAggregator.Summarize(estimates);

        Assert.Equal(3, summary.Count);
        Assert.Equal(15_000m, summary.TotalExtendedSupportCost);
        Assert.Equal(50_000m, summary.TotalMigrationCost);
        Assert.False(summary.HasMixedCurrencies);
    }

    [Fact]
    public void MixedCurrencies_FlagsMix_AndDoesNotSumIncompatibleFigures()
    {
        var estimates = new[]
        {
            Estimate(CostEstimateType.ExtendedSupport, 10_000m, "USD"),
            Estimate(CostEstimateType.ExtendedSupport, 8_000m, "EUR")
        };

        var summary = CostEstimateAggregator.Summarize(estimates);

        Assert.True(summary.HasMixedCurrencies);
        Assert.Equal(2, summary.CurrenciesUsed.Count);
        // Summing USD and EUR together would be meaningless, so the total is withheld rather than
        // silently adding two different currencies.
        Assert.Null(summary.TotalExtendedSupportCost);
    }

    [Fact]
    public void EstimateMissingReviewer_IsFlaggedSeparately()
    {
        var reviewed = Estimate(CostEstimateType.Migration, 1000m, reviewedBy: "Marcus Chen");
        var unreviewed = Estimate(CostEstimateType.Migration, 2000m, reviewedBy: "");

        var summary = CostEstimateAggregator.Summarize([reviewed, unreviewed]);

        Assert.Single(summary.MissingReviewer);
        Assert.Same(unreviewed, summary.MissingReviewer[0]);
    }

    [Fact]
    public void LatestEstimateDate_IsTheMostRecentAcrossAllEstimates()
    {
        var older = Estimate(CostEstimateType.ExtendedSupport, 1m);
        older.EstimateDateUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var newer = Estimate(CostEstimateType.Migration, 2m);
        newer.EstimateDateUtc = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

        var summary = CostEstimateAggregator.Summarize([older, newer]);

        Assert.Equal(newer.EstimateDateUtc, summary.LatestEstimateDateUtc);
    }
}
