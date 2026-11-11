namespace DBTracker.Core.Enums;

/// <summary>Outcome of one evidence check in a rehearsal or post-change validation. NotPerformed is the default and never counts as Passed.</summary>
public enum EvidenceCheckOutcome
{
    NotPerformed,
    Passed,
    Failed
}
