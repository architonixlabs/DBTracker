namespace DBTracker.Core.Entities;

/// <summary>Per-user read receipt for a (still shared/broadcast) Notification (U07) — read state is personal, so one user marking a notification read never affects what another user sees as unread.</summary>
public class NotificationReadState
{
    public int Id { get; set; }

    public int NotificationId { get; set; }
    public Notification? Notification { get; set; }

    public string UserId { get; set; } = string.Empty;

    public DateTime ReadAtUtc { get; set; } = DateTime.UtcNow;
}
