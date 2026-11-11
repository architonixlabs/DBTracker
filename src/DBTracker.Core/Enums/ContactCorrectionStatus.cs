namespace DBTracker.Core.Enums;

/// <summary>State of a contact correction request. Anyone signed in can request; only an authorized reviewer can approve or reject.</summary>
public enum ContactCorrectionStatus
{
    Pending,
    Approved,
    Rejected
}
