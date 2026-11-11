using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>Opts a user into a slice of alerts. Scope only narrows; it never surfaces an instance the user couldn't already see.</summary>
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

    /// <summary>Set when Scope is Team.</summary>
    public int? TeamId { get; set; }
    public Team? Team { get; set; }

    public NotificationChannel Channel { get; set; } = NotificationChannel.InApp;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
