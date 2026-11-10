using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class SupportRunwayNoticeEvaluatorTests
{
    private static readonly int[] Thresholds = [365, 180, 90, 30];

    [Fact]
    public void NullDays_NeverCrosses()
    {
        Assert.Null(SupportRunwayNoticeEvaluator.ThresholdCrossed(null, Thresholds));
        Assert.False(SupportRunwayNoticeEvaluator.ShouldNotify(null, Thresholds, null));
    }

    [Theory]
    [InlineData(400, null)]
    [InlineData(365, 365)]
    [InlineData(200, 365)]
    [InlineData(90, 90)]
    [InlineData(5, 30)]
    [InlineData(0, 0)]
    [InlineData(-3, 0)]
    public void ThresholdCrossed_PicksMostUrgentCrossed(int days, int? expected)
    {
        Assert.Equal(expected, SupportRunwayNoticeEvaluator.ThresholdCrossed(days, Thresholds));
    }

    [Fact]
    public void FirstEverNotice_FiresWhenAnyThresholdCrossed()
    {
        Assert.True(SupportRunwayNoticeEvaluator.ShouldNotify(300, Thresholds, null));
    }

    [Fact]
    public void SameThresholdAgain_DoesNotReNotify()
    {
        Assert.False(SupportRunwayNoticeEvaluator.ShouldNotify(170, Thresholds, 180));
    }

    [Fact]
    public void MoreUrgentThreshold_ReNotifies()
    {
        Assert.True(SupportRunwayNoticeEvaluator.ShouldNotify(80, Thresholds, 180));
    }

    [Fact]
    public void BecomingOverdueAfterThirtyDayNotice_StillNotifies()
    {
        Assert.True(SupportRunwayNoticeEvaluator.ShouldNotify(-1, Thresholds, 30));
    }

    [Fact]
    public void NoThresholdCrossedYet_DoesNotNotify()
    {
        Assert.False(SupportRunwayNoticeEvaluator.ShouldNotify(400, Thresholds, null));
    }

    [Fact]
    public void ParseThresholds_UsesDefaultsWhenUnconfigured()
    {
        var parsed = SupportRunwayNoticeEvaluator.ParseThresholds(null);
        Assert.Equal(new[] { 365, 180, 90, 30 }, parsed.OrderByDescending(x => x));
    }

    [Fact]
    public void ParseThresholds_ParsesConfiguredCsv()
    {
        var parsed = SupportRunwayNoticeEvaluator.ParseThresholds("60,14");
        Assert.Equal(new[] { 60, 14 }, parsed.OrderByDescending(x => x));
    }

    [Fact]
    public void ParseThresholds_IgnoresGarbageAndFallsBackToDefaults()
    {
        var parsed = SupportRunwayNoticeEvaluator.ParseThresholds("not,numbers,-5");
        Assert.Equal(new[] { 365, 180, 90, 30 }, parsed.OrderByDescending(x => x));
    }
}
