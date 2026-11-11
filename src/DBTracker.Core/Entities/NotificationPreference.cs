namespace DBTracker.Core.Entities;

/// <summary>A user's delivery preferences, one row per user. Today it holds quiet hours, which hold back alert email only:
/// the in-app inbox, incidents and escalation reminders carry on, so nothing is hidden from the record.</summary>
public class NotificationPreference
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public bool QuietHoursEnabled { get; set; }

    /// <summary>Minutes after local midnight in <see cref="TimeZoneId"/>.</summary>
    public int QuietStartMinute { get; set; } = 22 * 60;

    /// <summary>Minutes after local midnight. Earlier than the start means the window runs past midnight.</summary>
    public int QuietEndMinute { get; set; } = 7 * 60;

    /// <summary>System time zone id (Windows or IANA). Unknown ids fall back to UTC.</summary>
    public string TimeZoneId { get; set; } = string.Empty;

    /// <summary>When true, Failed (critical) alerts are still emailed during quiet hours.</summary>
    public bool CriticalBypassesQuietHours { get; set; } = true;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
