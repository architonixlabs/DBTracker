namespace DBTracker.Core.Enums;

/// <summary>Status of a readiness check. Unknown is the default and what a changed target or expired evidence falls back to; never treated as Passed.</summary>
public enum ReadinessCheckStatus
{
    Unknown,
    Passed,
    Failed,
    NotApplicable
}
