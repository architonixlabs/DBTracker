namespace DBTracker.Core.Enums;

/// <summary>A change window's lifecycle. Planned triggers nothing; Approved requires ChangeWindowApprovalGate to pass or an active emergency exception.</summary>
public enum ChangeWindowStatus
{
    Planned,
    Approved,
    InProgress,
    Completed,
    RolledBack,
    Cancelled
}
