using System.Globalization;

namespace DBTracker.Core.Services;

/// <summary>The result of turning a stated event time into UTC: the instant, the zone it was stated in, or an error to show.</summary>
public record EventTimeResult(DateTime? Utc, string ZoneId, string? Error)
{
    public bool Ok => Error is null && Utc is not null;
}

/// <summary>Turns times people state (a wall-clock time plus a named zone, or an ISO time with an offset) into UTC, and labels zones.
/// Never falls back to the server's own zone: a time without a zone is an error, not a guess.</summary>
public static class EventTimeZones
{
    public const string Utc = "UTC";

    /// <summary>Finds a zone by IANA id (Europe/London) or Windows id, or UTC. Null when the id is unknown.</summary>
    public static TimeZoneInfo? Find(string? zoneId)
    {
        if (string.IsNullOrWhiteSpace(zoneId))
        {
            return null;
        }

        var id = zoneId.Trim();
        if (string.Equals(id, Utc, StringComparison.OrdinalIgnoreCase) || string.Equals(id, "Z", StringComparison.OrdinalIgnoreCase)
            || string.Equals(id, "Etc/UTC", StringComparison.OrdinalIgnoreCase) || string.Equals(id, "GMT", StringComparison.OrdinalIgnoreCase))
        {
            return TimeZoneInfo.Utc;
        }

        return TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone) ? zone : null;
    }

    /// <summary>The zone id to store and show: UTC for the UTC zone, otherwise the id as given.</summary>
    public static string IdOf(TimeZoneInfo zone) => zone == TimeZoneInfo.Utc || zone.Id is "UTC" or "Etc/UTC" ? Utc : zone.Id;

    /// <summary>"UTC" or "Europe/London, UTC+01:00" at the given instant (the offset changes with daylight saving).</summary>
    public static string Label(TimeZoneInfo zone, DateTime atUtc)
    {
        var id = IdOf(zone);
        if (id == Utc)
        {
            return Utc;
        }

        return $"{id}, {OffsetLabel(zone.GetUtcOffset(DateTime.SpecifyKind(atUtc, DateTimeKind.Utc)))}";
    }

    public static string OffsetLabel(TimeSpan offset) =>
        offset == TimeSpan.Zero ? "UTC" : $"UTC{(offset < TimeSpan.Zero ? "-" : "+")}{offset.Duration():hh\\:mm}";

    /// <summary>Converts a UTC instant to the wall-clock time in a zone.</summary>
    public static DateTime ToZone(DateTime utc, TimeZoneInfo zone) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone);

    /// <summary>Converts a wall-clock time stated in a zone to UTC. A time skipped by a daylight-saving change is an error;
    /// a repeated (ambiguous) time uses the zone's standard offset, the earlier reading is not guessed.</summary>
    public static EventTimeResult FromZone(DateTime wallClock, TimeZoneInfo zone)
    {
        var unspecified = DateTime.SpecifyKind(wallClock, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(unspecified))
        {
            return new EventTimeResult(null, IdOf(zone), $"{unspecified:yyyy-MM-dd HH:mm} doesn't exist in {IdOf(zone)} (the clocks skipped it for daylight saving).");
        }

        var utc = TimeZoneInfo.ConvertTimeToUtc(unspecified, zone);
        return new EventTimeResult(DateTime.SpecifyKind(utc, DateTimeKind.Utc), IdOf(zone), null);
    }

    /// <summary>Validates that an event time is not in the future (one minute of clock skew is allowed).</summary>
    public static string? FutureError(DateTime utc, DateTime nowUtc) =>
        utc > nowUtc.AddMinutes(1) ? "Event time can't be in the future." : null;

    /// <summary>A form's "occurred at" field: a wall-clock time in the chosen zone, rejected when it is in the future or skipped by
    /// daylight saving. <paramref name="untouchedDefault"/> means the person left the "now" default (shown rounded to the minute),
    /// so it is taken as now rather than up to 59 seconds earlier.</summary>
    public static EventTimeResult FromEntryForm(DateTime wallClock, TimeZoneInfo zone, bool untouchedDefault, DateTime nowUtc)
    {
        if (untouchedDefault)
        {
            return new EventTimeResult(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), IdOf(zone), null);
        }

        var result = FromZone(wallClock, zone);
        if (result.Error is not null)
        {
            return result;
        }

        return FutureError(result.Utc!.Value, nowUtc) is { } future ? result with { Utc = null, Error = future } : result;
    }

    private static readonly string[] WallClockFormats =
    [
        "yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:ss.FFFFFFF", "yyyy-MM-dd HH:mm:ss.FFFFFFF"
    ];

    /// <summary>Parses an imported event time. Accepts an ISO 8601 time with an offset or Z (2026-10-01T02:00:00+01:00), or a
    /// wall-clock time (2026-10-01 02:00) together with a time zone. A time with neither is rejected; when both are given they must agree.</summary>
    public static EventTimeResult Parse(string? rawTime, string? rawZone)
    {
        var time = (rawTime ?? "").Trim();
        var zoneText = (rawZone ?? "").Trim();
        if (time.Length == 0)
        {
            return new EventTimeResult(null, "", "EventTime is required.");
        }

        TimeZoneInfo? zone = null;
        if (zoneText.Length > 0)
        {
            zone = Find(zoneText);
            if (zone is null)
            {
                return new EventTimeResult(null, "", $"TimeZone '{zoneText}' isn't a known time zone. Use an IANA name such as Europe/London, or UTC.");
            }
        }

        if (HasExplicitOffset(time)
            && DateTimeOffset.TryParse(time, CultureInfo.InvariantCulture, DateTimeStyles.None, out var withOffset))
        {
            var utc = withOffset.UtcDateTime;
            if (zone is not null && zone.GetUtcOffset(utc) != withOffset.Offset)
            {
                return new EventTimeResult(null, "",
                    $"EventTime offset {OffsetLabel(withOffset.Offset)} doesn't match {IdOf(zone)} ({OffsetLabel(zone.GetUtcOffset(utc))} at that time). Give the zone once, or make them agree.");
            }

            var zoneId = zone is not null ? IdOf(zone) : (withOffset.Offset == TimeSpan.Zero ? Utc : OffsetLabel(withOffset.Offset));
            return new EventTimeResult(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zoneId, null);
        }

        if (!DateTime.TryParseExact(time, WallClockFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var wallClock))
        {
            return new EventTimeResult(null, "", $"EventTime '{time}' isn't a date and time. Use yyyy-MM-dd HH:mm, or ISO 8601 with an offset such as 2026-10-01T02:00:00+01:00.");
        }

        if (zone is null)
        {
            return new EventTimeResult(null, "", $"EventTime '{time}' has no time zone. Add an offset (+01:00 or Z) or fill in the TimeZone column.");
        }

        return FromZone(wallClock, zone);
    }

    /// <summary>True when the text ends in Z or a +hh:mm / -hh:mm offset after the time part.</summary>
    private static bool HasExplicitOffset(string time)
    {
        if (time.EndsWith('Z') || time.EndsWith('z'))
        {
            return true;
        }

        var timeStart = time.IndexOfAny(['T', ' ']);
        if (timeStart < 0)
        {
            return false;
        }

        var tail = time[(timeStart + 1)..];
        return tail.Contains('+') || tail.Contains('-');
    }
}
