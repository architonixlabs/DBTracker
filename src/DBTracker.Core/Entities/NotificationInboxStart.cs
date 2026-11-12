namespace DBTracker.Core.Entities;

/// <summary>When a user first opened their personal inbox. Notifications created before this are marked read once;
/// anything later stays unread until the user reads it.</summary>
public class NotificationInboxStart
{
    public string UserId { get; set; } = string.Empty;

    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
}
