using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>An in-app alert, typically raised when an instance's backup health degrades to Overdue or Failed.</summary>
public class Notification
{
    public int Id { get; set; }

    public int? DatabaseInstanceId { get; set; }
    public DatabaseInstance? DatabaseInstance { get; set; }

    /// <summary>Instance name and company at alert time, kept after archive; Company-scoped subscriptions match on it.</summary>
    public string InstanceName { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;

    /// <summary>Owner team at alert time, so a later reassignment doesn't change which past alerts a team subscription matches.</summary>
    public int? OwnerTeamId { get; set; }

    public string Message { get; set; } = string.Empty;
    public NotificationSeverity Severity { get; set; } = NotificationSeverity.Info;
    public bool IsRead { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
