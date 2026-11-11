using System.Globalization;

namespace DBTracker.Core.Services;

/// <summary>Whether a user's quiet hours cover an instant. Quiet hours only hold back email; the in-app inbox, incidents and escalation carry on.</summary>
public static class QuietHoursEvaluator
{
    public const int MinutesPerDay = 24 * 60;

    /// <param name="startMinute">Minutes after local midnight when quiet hours start (0 to 1439).</param>
    /// <param name="endMinute">Minutes after local midnight when they end. Earlier than start means the window runs past midnight; equal to start means no quiet hours.</param>
    /// <param name="timeZone">The user's zone. Quiet hours follow its local clock, including daylight saving changes.</param>
    public static bool IsQuiet(bool enabled, int startMinute, int endMinute, TimeZoneInfo timeZone, DateTime nowUtc)
    {
        if (!enabled || !IsValidMinute(startMinute) || !IsValidMinute(endMinute) || startMinute == endMinute)
        {
            return false;
        }

        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), timeZone);
        var minute = local.Hour * 60 + local.Minute;

        return startMinute < endMinute
            ? minute >= startMinute && minute < endMinute
            : minute >= startMinute || minute < endMinute;
    }

    /// <summary>Critical (Failed) alerts can be let through quiet hours; recovery and other Info messages never are.</summary>
    public static bool ShouldHoldEmail(bool isQuiet, bool isCritical, bool criticalBypassesQuietHours) =>
        isQuiet && !(isCritical && criticalBypassesQuietHours);

    public static bool IsValidMinute(int minute) => minute is >= 0 and < MinutesPerDay;

    /// <summary>Parses "HH:mm" (24-hour) into minutes after midnight.</summary>
    public static bool TryParseClock(string? value, out int minute)
    {
        minute = 0;
        if (TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var t))
        {
            minute = t.Hour * 60 + t.Minute;
            return true;
        }

        return false;
    }

    public static string FormatClock(int minute) =>
        IsValidMinute(minute) ? $"{minute / 60:00}:{minute % 60:00}" : "00:00";

    /// <summary>Resolves a stored zone id, falling back to UTC when the id is unknown on this server.</summary>
    public static TimeZoneInfo ResolveZone(string? timeZoneId) =>
        !string.IsNullOrWhiteSpace(timeZoneId) && TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var zone)
            ? zone
            : TimeZoneInfo.Utc;
}
