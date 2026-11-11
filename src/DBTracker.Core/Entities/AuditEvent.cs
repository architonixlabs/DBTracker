using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>A general-purpose audit entry: actor, time, action, target, and optional reason and before/after JSON.</summary>
/// <remarks>Application-level log only: anyone with direct database access can alter or delete rows.</remarks>
public class AuditEvent
{
    public int Id { get; set; }

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    public AuditActorType ActorType { get; set; } = AuditActorType.User;

    /// <summary>Identity user id, when ActorType is User and the actor is a known account. Empty for System actors.</summary>
    public string ActorUserId { get; set; } = string.Empty;

    /// <summary>Display name/email of the actor, or a fixed label (e.g. "Background: AlertCheckHostedService") for System actors.</summary>
    public string ActorName { get; set; } = string.Empty;

    /// <summary>Short machine-stable verb, e.g. "InstanceArchived", "UserRoleChanged", "BulkEditApplied".</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>What kind of record was acted on, e.g. "DatabaseInstance", "User", "CsvImport".</summary>
    public string TargetType { get; set; } = string.Empty;

    /// <summary>The target's id/key as a string — instances use their int Id, users their Identity string id.</summary>
    public string TargetId { get; set; } = string.Empty;

    /// <summary>Human-readable label for the target (instance name, user email, "47 instances") so the audit view doesn't require a join to be useful.</summary>
    public string TargetName { get; set; } = string.Empty;

    /// <summary>Why the action was taken, when applicable (archive reason, correction reason, …). Empty when not required.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Optional JSON snapshot of relevant fields before the change.</summary>
    public string? BeforeJson { get; set; }

    /// <summary>Optional JSON snapshot of relevant fields after the change.</summary>
    public string? AfterJson { get; set; }
}
