using System.Globalization;

namespace DBTracker.Core.Services;

/// <summary>The Reports page date window as it round-trips through the URL: a preset (7, 30 or 90 days) or custom local calendar dates.</summary>
/// <remarks>Invalid values fall back to the 7-day preset rather than erroring, so an edited or stale link still opens a usable page.</remarks>
public sealed record ReportRangeQuery(int PresetDays, bool IsCustom, DateOnly From, DateOnly To)
{
    public static readonly int[] Presets = [7, 30, 90];
    public const int DefaultPresetDays = 7;

    /// <summary>Longest custom window accepted (about ten years), so a typo can't ask for every row since year 1.</summary>
    public const int MaxCustomDays = 3660;

    private const string DateFormat = "yyyy-MM-dd";

    public static ReportRangeQuery Default(DateOnly today) =>
        new(DefaultPresetDays, false, today.AddDays(-DefaultPresetDays), today);

    public static ReportRangeQuery Preset(int days, DateOnly today) =>
        Presets.Contains(days) ? new(days, false, today.AddDays(-days), today) : Default(today);

    /// <summary>Null when the dates are reversed or span more than <see cref="MaxCustomDays"/>.</summary>
    public static ReportRangeQuery? Custom(DateOnly from, DateOnly to)
    {
        if (from > to || to.DayNumber - from.DayNumber > MaxCustomDays)
        {
            return null;
        }

        return new(DefaultPresetDays, true, from, to);
    }

    /// <param name="days">The "days" query value, used when no valid custom range is given.</param>
    /// <param name="from">The "from" query value, yyyy-MM-dd.</param>
    /// <param name="to">The "to" query value, yyyy-MM-dd, inclusive.</param>
    public static ReportRangeQuery Parse(string? days, string? from, string? to, DateOnly today)
    {
        if (TryParseDate(from, out var f) && TryParseDate(to, out var t) && Custom(f, t) is { } custom)
        {
            return custom;
        }

        return int.TryParse(days, NumberStyles.None, CultureInfo.InvariantCulture, out var d)
            ? Preset(d, today)
            : Default(today);
    }

    /// <summary>Query-string fragment (no leading ? or &amp;) that <see cref="Parse"/> reads back to the same range.</summary>
    public string ToQueryString() =>
        IsCustom
            ? $"from={From.ToString(DateFormat, CultureInfo.InvariantCulture)}&to={To.ToString(DateFormat, CultureInfo.InvariantCulture)}"
            : $"days={PresetDays}";

    public string Describe() =>
        IsCustom
            ? $"{From.ToString("MMM d, yyyy", CultureInfo.InvariantCulture)} to {To.ToString("MMM d, yyyy", CultureInfo.InvariantCulture)}"
            : $"the last {PresetDays} days";

    private static bool TryParseDate(string? value, out DateOnly date) =>
        DateOnly.TryParseExact(value, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
}
