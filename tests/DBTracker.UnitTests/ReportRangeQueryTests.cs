using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class ReportRangeQueryTests
{
    private static readonly DateOnly Today = new(2026, 11, 11);

    [Fact]
    public void NoQuery_DefaultsToSevenDays()
    {
        var range = ReportRangeQuery.Parse(null, null, null, Today);
        Assert.False(range.IsCustom);
        Assert.Equal(7, range.PresetDays);
        Assert.Equal("days=7", range.ToQueryString());
    }

    [Theory]
    [InlineData("30", 30)]
    [InlineData("90", 90)]
    [InlineData("14", 7)]
    [InlineData("-30", 7)]
    [InlineData("abc", 7)]
    public void Days_OnlyAcceptsPresets(string days, int expected)
    {
        Assert.Equal(expected, ReportRangeQuery.Parse(days, null, null, Today).PresetDays);
    }

    [Fact]
    public void CustomRange_RoundTripsThroughQueryString()
    {
        var range = ReportRangeQuery.Parse("30", "2026-10-01", "2026-10-31", Today);

        Assert.True(range.IsCustom);
        Assert.Equal(new DateOnly(2026, 10, 1), range.From);
        Assert.Equal(new DateOnly(2026, 10, 31), range.To);
        Assert.Equal("from=2026-10-01&to=2026-10-31", range.ToQueryString());

        var parts = range.ToQueryString().Split('&').Select(p => p.Split('=')).ToDictionary(p => p[0], p => p[1]);
        Assert.Equal(range, ReportRangeQuery.Parse(null, parts["from"], parts["to"], Today));
    }

    [Fact]
    public void SingleDayCustomRange_IsValid()
    {
        Assert.True(ReportRangeQuery.Parse(null, "2026-11-01", "2026-11-01", Today).IsCustom);
    }

    [Theory]
    [InlineData("2026-10-31", "2026-10-01")] // reversed
    [InlineData("2026-13-01", "2026-12-01")] // invalid month
    [InlineData("10/01/2026", "10/31/2026")] // wrong format
    [InlineData("2026-10-01", null)]          // half a range
    [InlineData("2000-01-01", "2026-01-01")]  // longer than the limit
    public void InvalidCustomRange_FallsBackToPreset(string? from, string? to)
    {
        var range = ReportRangeQuery.Parse("30", from, to, Today);
        Assert.False(range.IsCustom);
        Assert.Equal(30, range.PresetDays);
    }

    [Fact]
    public void Custom_RejectsReversedDates()
    {
        Assert.Null(ReportRangeQuery.Custom(new DateOnly(2026, 2, 1), new DateOnly(2026, 1, 1)));
    }
}
