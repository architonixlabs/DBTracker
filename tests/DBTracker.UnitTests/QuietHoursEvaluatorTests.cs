using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class QuietHoursEvaluatorTests
{
    private static DateTime Utc(int hour, int minute = 0) => new(2026, 11, 11, hour, minute, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(23, 0, true)]
    [InlineData(2, 30, true)]
    [InlineData(6, 59, true)]
    [InlineData(7, 0, false)]
    [InlineData(21, 59, false)]
    [InlineData(22, 0, true)]
    public void OvernightWindow_WrapsPastMidnight(int hour, int minute, bool expected)
    {
        Assert.Equal(expected, QuietHoursEvaluator.IsQuiet(true, 22 * 60, 7 * 60, TimeZoneInfo.Utc, Utc(hour, minute)));
    }

    [Theory]
    [InlineData(12, 30, true)]
    [InlineData(11, 59, false)]
    [InlineData(14, 0, false)]
    public void SameDayWindow(int hour, int minute, bool expected)
    {
        Assert.Equal(expected, QuietHoursEvaluator.IsQuiet(true, 12 * 60, 14 * 60, TimeZoneInfo.Utc, Utc(hour, minute)));
    }

    [Fact]
    public void Disabled_OrEmptyWindow_IsNeverQuiet()
    {
        Assert.False(QuietHoursEvaluator.IsQuiet(false, 0, 23 * 60, TimeZoneInfo.Utc, Utc(3)));
        Assert.False(QuietHoursEvaluator.IsQuiet(true, 8 * 60, 8 * 60, TimeZoneInfo.Utc, Utc(8)));
        Assert.False(QuietHoursEvaluator.IsQuiet(true, -1, 8 * 60, TimeZoneInfo.Utc, Utc(3)));
    }

    [Fact]
    public void FollowsTheUsersZone_NotUtc()
    {
        // 03:00 UTC is 22:00 the previous evening at UTC-5.
        var minusFive = TimeZoneInfo.CreateCustomTimeZone("Test-5", TimeSpan.FromHours(-5), "Test-5", "Test-5");
        Assert.True(QuietHoursEvaluator.IsQuiet(true, 21 * 60, 23 * 60, minusFive, Utc(3)));
        Assert.False(QuietHoursEvaluator.IsQuiet(true, 21 * 60, 23 * 60, TimeZoneInfo.Utc, Utc(3)));
    }

    [Theory]
    [InlineData(true, true, true, false)]   // quiet, critical, bypass on: send
    [InlineData(true, true, false, true)]   // quiet, critical, bypass off: hold
    [InlineData(true, false, true, true)]   // quiet, warning: hold
    [InlineData(false, false, false, false)] // not quiet: send
    public void ShouldHoldEmail(bool isQuiet, bool isCritical, bool bypass, bool expected)
    {
        Assert.Equal(expected, QuietHoursEvaluator.ShouldHoldEmail(isQuiet, isCritical, bypass));
    }

    [Fact]
    public void ClockParsingAndFormatting_RoundTrip()
    {
        Assert.True(QuietHoursEvaluator.TryParseClock("22:30", out var minute));
        Assert.Equal(22 * 60 + 30, minute);
        Assert.Equal("22:30", QuietHoursEvaluator.FormatClock(minute));
        Assert.False(QuietHoursEvaluator.TryParseClock("25:00", out _));
    }

    [Fact]
    public void UnknownZone_FallsBackToUtc()
    {
        Assert.Equal(TimeZoneInfo.Utc, QuietHoursEvaluator.ResolveZone("Not/AZone"));
        Assert.Equal(TimeZoneInfo.Utc, QuietHoursEvaluator.ResolveZone(""));
    }
}
