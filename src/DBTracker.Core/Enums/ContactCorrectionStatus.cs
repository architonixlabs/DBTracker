namespace DBTracker.Core.Enums;

/// <summary>State of a requested contact correction (U21) — any authenticated viewer can request one; only an authorized reviewer can approve/reject it.</summary>
public enum ContactCorrectionStatus
{
    Pending,
    Approved,
    Rejected
}
