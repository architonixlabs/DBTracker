using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>An in-app alert, typically raised when an instance's backup health degrades to Overdue or Failed.</summary>
public class Notification
{
    public int Id { get; set; }

    public int? DatabaseInstanceId { get; set; }
    public DatabaseInstance? DatabaseInstance { get; set; }

    /// <summary>Instance name/company at the time of the alert, kept even if the instance is later archived/removed — also what NotificationSubscriptionMatcher (U07) matches a Company-scoped subscription against.</summary>
    public string InstanceName { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;

    /// <summary>Owner team at the time of the alert (U21), denormalized the same way Company already is — so a later team reassignment doesn't retroactively change which past alerts a Team-scoped subscription matches.</summary>
    public int? OwnerTeamId { get; set; }

    public string Message { get; set; } = string.Empty;
    public NotificationSeverity Severity { get; set; } = NotificationSeverity.Info;
    public bool IsRead { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
