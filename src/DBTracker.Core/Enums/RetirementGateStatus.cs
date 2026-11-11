namespace DBTracker.Core.Enums;

/// <summary>
/// D22 — the status of one gate on a RetirementChecklist. Unknown (nothing recorded yet to confirm
/// one way or the other) and NotApplicable (the gate genuinely doesn't apply, e.g. no active change to
/// close a rollback window on) are both first-class, distinct from Blocked (something was actually
/// checked and failed) — neither Unknown nor NotApplicable is ever silently promoted to Passed.
/// </summary>
public enum RetirementGateStatus
{
    Blocked,
    Unknown,
    NotApplicable,
    Passed
}
