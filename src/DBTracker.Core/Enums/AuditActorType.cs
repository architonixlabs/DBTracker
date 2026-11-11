namespace DBTracker.Core.Enums;

/// <summary>Who performed an audited action, so a background sweep is never mistaken for a human decision.</summary>
public enum AuditActorType
{
    /// <summary>A background worker or the application itself (e.g. the alert sweep opening an incident).</summary>
    System,

    /// <summary>An authenticated human user.</summary>
    User
}
