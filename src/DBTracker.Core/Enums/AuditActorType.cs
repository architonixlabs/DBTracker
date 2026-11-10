namespace DBTracker.Core.Enums;

/// <summary>Who performed an audited action (U19) — kept distinct so a background sweep's own
/// actions are never confused with a human decision when reviewing the audit trail.</summary>
public enum AuditActorType
{
    /// <summary>A background worker or the application itself (e.g. the alert sweep opening an incident).</summary>
    System,

    /// <summary>An authenticated human user.</summary>
    User
}
