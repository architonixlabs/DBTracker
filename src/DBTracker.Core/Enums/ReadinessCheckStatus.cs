namespace DBTracker.Core.Enums;

/// <summary>
/// D09 — an un-performed check is Unknown by construction (it's the default and what a changed
/// target/expired evidence falls back to); it is never silently treated as Passed.
/// </summary>
public enum ReadinessCheckStatus
{
    Unknown,
    Passed,
    Failed,
    NotApplicable
}
