namespace DBTracker.Core.Enums;

/// <summary>An instance's patch-baseline coverage. Unknown (no resolved identity or catalogue match) differs from NoBaselineSet (no approved target yet); neither is shown as compliant.</summary>
public enum LifecycleCoverageStatus
{
    Unknown,
    NoBaselineSet,
    OnApprovedBaseline,
    BehindBaseline
}
