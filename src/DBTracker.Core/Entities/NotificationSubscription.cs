using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>Opts a user into a slice of alerts (U07) — scope only ever narrows what a broadcast notification already covers, it can never surface an instance the viewer wasn't already able to see.</summary>
public class NotificationSubscription
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public SubscriptionScope Scope { get; set; } = SubscriptionScope.AllInstances;

    /// <summary>Set when Scope is Instance.</summary>
    public int? DatabaseInstanceId { get; set; }
    public DatabaseInstance? DatabaseInstance { get; set; }

    /// <summary>Set when Scope is Company.</summary>
    public string Company { get; set; } = string.Empty;

    /// <summary>Set when Scope is Team (U21) — instances owned by this team.</summary>
    public int? TeamId { get; set; }
    public Team? Team { get; set; }

    public NotificationChannel Channel { get; set; } = NotificationChannel.InApp;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
