using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class CapacityForecastBuilderTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

    private static CapacityMeasurement Growth(DateTime measuredAtUtc, double value) =>
        new() { MeasurementType = CapacityMeasurementType.Growth, MeasuredAtUtc = measuredAtUtc, Value = value, Unit = "GB" };

    [Fact]
    public void FewerThanTwoMeasurements_IsInsufficient_AndFlagsTheGap()
    {
        var result = CapacityForecastBuilder.Build([Growth(Now, 100)], 500, Now);

        Assert.False(result.HasSufficientData);
        Assert.Contains(result.MissingDataGaps, g => g.Contains("two timestamped"));
    }

    [Fact]
    public void NoCapacityLimit_StillReturnsRate_ButNoExhaustionDate_AndFlagsTheGap()
    {
        var measurements = new[] { Growth(Now.AddDays(-30), 100), Growth(Now, 130) };

        var result = CapacityForecastBuilder.Build(measurements, null, Now);

        Assert.False(result.HasSufficientData);
        Assert.NotNull(result.ProjectedGrowthPerDayGb);
        Assert.Null(result.EarliestPlausibleExhaustionUtc);
        Assert.Contains(result.MissingDataGaps, g => g.Contains("capacity limit"));
    }

    [Fact]
    public void SufficientData_ProjectsRangeNotASinglePreciseDate()
    {
        var measurements = new[]
        {
            Growth(Now.AddDays(-60), 100),
            Growth(Now.AddDays(-30), 115), // slower interval: 0.5 GB/day
            Growth(Now, 160)               // faster interval: 1.5 GB/day
        };

        var result = CapacityForecastBuilder.Build(measurements, 500, Now);

        Assert.True(result.HasSufficientData);
        Assert.NotNull(result.EarliestPlausibleExhaustionUtc);
        Assert.NotNull(result.LatestPlausibleExhaustionUtc);
        Assert.True(result.EarliestPlausibleExhaustionUtc <= result.LatestPlausibleExhaustionUtc);
        Assert.NotEmpty(result.Assumptions);
    }

    [Fact]
    public void AlreadyAtOrOverLimit_FlagsImmediateExhaustion()
    {
        var measurements = new[] { Growth(Now.AddDays(-10), 490), Growth(Now, 520) };

        var result = CapacityForecastBuilder.Build(measurements, 500, Now);

        Assert.True(result.HasSufficientData);
        Assert.Equal(Now, result.EarliestPlausibleExhaustionUtc);
        Assert.Contains(result.MissingDataGaps, g => g.Contains("already at or beyond"));
    }

    [Fact]
    public void FlatOrNegativeGrowth_ProjectsNoExhaustion()
    {
        var measurements = new[] { Growth(Now.AddDays(-10), 200), Growth(Now, 190) };

        var result = CapacityForecastBuilder.Build(measurements, 500, Now);

        Assert.True(result.HasSufficientData);
        Assert.Null(result.EarliestPlausibleExhaustionUtc);
        Assert.Contains(result.MissingDataGaps, g => g.Contains("flat or negative"));
    }

    [Fact]
    public void AssumptionsAreAlwaysStated_EvenWhenDataIsInsufficient()
    {
        var result = CapacityForecastBuilder.Build([], null, Now);
        Assert.NotEmpty(result.Assumptions);
    }
}
