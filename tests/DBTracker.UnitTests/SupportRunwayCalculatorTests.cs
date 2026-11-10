using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class SupportRunwayCalculatorTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void NoMatchingEntry_IsUnknown()
    {
        var result = SupportRunwayCalculator.Compute(null, Now);
        Assert.True(result.IsUnknown);
        Assert.Equal(SupportPhase.Unknown, result.Phase);
    }

    [Fact]
    public void TbdDates_IsUnknown()
    {
        var entry = new ReleaseCatalogEntry { DatesAreTbd = true, MainstreamSupportEndUtc = Now.AddDays(30) };
        var result = SupportRunwayCalculator.Compute(entry, Now);
        Assert.True(result.IsUnknown);
    }

    [Fact]
    public void ConflictingDates_IsUnknown()
    {
        var entry = new ReleaseCatalogEntry { DatesConflict = true, MainstreamSupportEndUtc = Now.AddDays(30) };
        var result = SupportRunwayCalculator.Compute(entry, Now);
        Assert.True(result.IsUnknown);
    }

    [Fact]
    public void NoUsableMilestones_IsUnknown()
    {
        var entry = new ReleaseCatalogEntry();
        var result = SupportRunwayCalculator.Compute(entry, Now);
        Assert.True(result.IsUnknown);
    }

    [Fact]
    public void MilestoneTomorrow_ReportsOneDayRemaining_InMainstreamPhase()
    {
        var entry = new ReleaseCatalogEntry { MainstreamSupportEndUtc = Now.AddDays(1) };
        var result = SupportRunwayCalculator.Compute(entry, Now);
        Assert.False(result.IsUnknown);
        Assert.Equal(SupportPhase.Mainstream, result.Phase);
        Assert.Equal(1, result.DaysToNextMilestone);
    }

    [Fact]
    public void MilestoneToday_ReportsZeroDaysRemaining()
    {
        var entry = new ReleaseCatalogEntry { MainstreamSupportEndUtc = Now };
        var result = SupportRunwayCalculator.Compute(entry, Now);
        Assert.Equal(0, result.DaysToNextMilestone);
    }

    [Fact]
    public void MilestoneYesterday_IsEndOfLife_WithNegativeOverdueDays()
    {
        var entry = new ReleaseCatalogEntry { MainstreamSupportEndUtc = Now.AddDays(-1) };
        var result = SupportRunwayCalculator.Compute(entry, Now);
        Assert.False(result.IsUnknown);
        Assert.Equal(SupportPhase.EndOfLife, result.Phase);
        Assert.True(result.DaysToNextMilestone < 0);
    }

    [Fact]
    public void MainstreamPassedButExtendedUpcoming_ReportsExtendedPhase()
    {
        var entry = new ReleaseCatalogEntry
        {
            MainstreamSupportEndUtc = Now.AddDays(-30),
            ExtendedSupportEndUtc = Now.AddDays(60)
        };
        var result = SupportRunwayCalculator.Compute(entry, Now);
        Assert.Equal(SupportPhase.Extended, result.Phase);
        Assert.Equal(60, result.DaysToNextMilestone);
    }

    [Fact]
    public void UnknownPrecisionMilestone_Ignored()
    {
        var entry = new ReleaseCatalogEntry
        {
            MainstreamSupportEndUtc = Now.AddDays(10),
            MainstreamSupportEndPrecision = DatePrecision.Unknown,
            ExtendedSupportEndUtc = Now.AddDays(90)
        };
        var result = SupportRunwayCalculator.Compute(entry, Now);
        Assert.Equal(SupportPhase.Extended, result.Phase);
    }
}
