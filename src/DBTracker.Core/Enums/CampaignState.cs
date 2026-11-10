namespace DBTracker.Core.Enums;

/// <summary>D10 migration-campaign lifecycle state. Valid transitions are enforced by CampaignStateTransitionValidator, not by the enum itself.</summary>
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
