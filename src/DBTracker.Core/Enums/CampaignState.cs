namespace DBTracker.Core.Enums;

/// <summary>Migration campaign state. Valid transitions are enforced by CampaignStateTransitionValidator.</summary>
public enum CampaignState
{
    Proposed,
    Assessing,
    Blocked,
    Ready,
    Scheduled,
    InProgress,
    Validating,
    Completed,
    RolledBack,
    Cancelled
}
