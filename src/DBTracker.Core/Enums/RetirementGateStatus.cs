namespace DBTracker.Core.Enums;

/// <summary>Status of one retirement gate. Unknown (nothing recorded) and NotApplicable are distinct from Blocked (checked and failed), and neither is ever promoted to Passed.</summary>
public enum RetirementGateStatus
{
    Blocked,
    Unknown,
    NotApplicable,
    Passed
}
