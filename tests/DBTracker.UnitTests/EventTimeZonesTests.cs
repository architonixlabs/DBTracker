using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

/// <summary>G11: stated event times always carry an explicit zone and never fall back to the server's zone.</summary>
public class EventTimeZonesTests
{
    [Fact]
    public void IsoTimeWithOffset_IsConvertedToUtc_AndKeepsTheOffsetAsTheStatedZone()
    {
        var result = EventTimeZones.Parse("2026-10-01T02:00:00+01:00", "");

        Assert.True(result.Ok);
        Assert.Equal(new DateTime(2026, 10, 1, 1, 0, 0, DateTimeKind.Utc), result.Utc);
        Assert.Equal("UTC+01:00", result.ZoneId);
    }

    [Fact]
    public void ZuluTime_IsUtc()
    {
        var result = EventTimeZones.Parse("2026-10-15T09:30:00Z", null);

        Assert.Equal(new DateTime(2026, 10, 15, 9, 30, 0, DateTimeKind.Utc), result.Utc);
        Assert.Equal("UTC", result.ZoneId);
    }

    [Fact]
    public void WallClockTime_WithNamedZone_UsesThatZonesOffsetOnThatDate()
    {
        // London is on BST (UTC+1) on 1 October and on GMT in December.
        var summer = EventTimeZones.Parse("2026-10-01 02:00", "Europe/London");
        var winter = EventTimeZones.Parse("2026-12-01 02:00", "Europe/London");

        Assert.Equal(new DateTime(2026, 10, 1, 1, 0, 0, DateTimeKind.Utc), summer.Utc);
        Assert.Equal(new DateTime(2026, 12, 1, 2, 0, 0, DateTimeKind.Utc), winter.Utc);
        Assert.Equal("Europe/London", summer.ZoneId);
    }

    [Fact]
    public void WallClockTime_WithoutAnyZone_IsAnError_NotAGuess()
    {
        var result = EventTimeZones.Parse("2026-10-01 02:00", "");

        Assert.False(result.Ok);
        Assert.Contains("has no time zone", result.Error);
    }

    [Fact]
    public void UnknownZone_IsAnError()
    {
        var result = EventTimeZones.Parse("2026-10-01 02:00", "Mars/Olympus");

        Assert.Contains("isn't a known time zone", result.Error);
    }

    [Fact]
    public void OffsetThatDisagreesWithTheNamedZone_IsAnError()
    {
        var result = EventTimeZones.Parse("2026-10-01T02:00:00+05:00", "Europe/London");

        Assert.Contains("doesn't match", result.Error);
    }

    [Fact]
    public void OffsetThatAgreesWithTheNamedZone_IsAccepted_AndStoresTheZoneName()
    {
        var result = EventTimeZones.Parse("2026-10-01T02:00:00+01:00", "Europe/London");

        Assert.True(result.Ok);
        Assert.Equal("Europe/London", result.ZoneId);
    }

    [Fact]
    public void TimeSkippedByDaylightSaving_IsRejected()
    {
        // Clocks in London jump from 01:00 to 02:00 on 29 March 2026.
        var result = EventTimeZones.Parse("2026-03-29 01:30", "Europe/London");

        Assert.Contains("doesn't exist", result.Error);
    }

    [Fact]
    public void Garbage_IsRejectedWithTheExpectedFormats()
    {
        Assert.Contains("yyyy-MM-dd HH:mm", EventTimeZones.Parse("yesterday", "UTC").Error);
        Assert.Contains("EventTime is required", EventTimeZones.Parse("", "UTC").Error);
    }

    [Fact]
    public void FutureTimes_AreRejected_WithOneMinuteOfSkew()
    {
        var now = new DateTime(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);

        Assert.Null(EventTimeZones.FutureError(now.AddSeconds(30), now));
        Assert.NotNull(EventTimeZones.FutureError(now.AddMinutes(5), now));
    }

    [Fact]
    public void Label_NamesTheZoneAndItsOffsetAtThatInstant()
    {
        var london = EventTimeZones.Find("Europe/London")!;

        Assert.Equal("Europe/London, UTC+01:00", EventTimeZones.Label(london, new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)));
        Assert.Equal("UTC", EventTimeZones.Label(TimeZoneInfo.Utc, DateTime.UtcNow));
        Assert.Equal("UTC-05:00", EventTimeZones.OffsetLabel(TimeSpan.FromHours(-5)));
    }

    [Fact]
    public void FromZone_RoundTripsWithToZone()
    {
        var ny = EventTimeZones.Find("America/New_York")!;
        var utc = new DateTime(2026, 11, 11, 14, 5, 0, DateTimeKind.Utc);

        var local = EventTimeZones.ToZone(utc, ny);
        Assert.Equal(new DateTime(2026, 11, 11, 9, 5, 0), local);
        Assert.Equal(utc, EventTimeZones.FromZone(local, ny).Utc);
    }
}
