namespace DBTracker.Core.Entities;

/// <summary>Per-user read receipt for a shared notification, so one user's read never clears another's.</summary>
public class NotificationReadState
{
    public int Id { get; set; }

    public int NotificationId { get; set; }
    public Notification? Notification { get; set; }

    public string UserId { get; set; } = string.Empty;

    public DateTime ReadAtUtc { get; set; } = DateTime.UtcNow;
}
