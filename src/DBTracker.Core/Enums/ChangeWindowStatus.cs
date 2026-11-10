namespace DBTracker.Core.Enums;

/// <summary>D12 — a scheduled change window's own lifecycle. Creating a window at Planned never executes anything or sends any notification by itself; moving to Approved requires ChangeWindowApprovalGate to pass (or a linked, active D14 emergency exception).</summary>
public enum ChangeWindowStatus
{
    Planned,
    Approved,
    InProgress,
    Completed,
    RolledBack,
    Cancelled
}
