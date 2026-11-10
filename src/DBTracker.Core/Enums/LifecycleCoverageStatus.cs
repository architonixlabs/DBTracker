namespace DBTracker.Core.Enums;

/// <summary>D15 — one instance's patch-baseline coverage position, summarized for the lifecycle workspace/reports from D04's PatchBaselineComparer facts. Unknown (no resolved identity, or no catalogue match) is distinct from NoBaselineSet (identity resolved, but nobody has reviewed/approved a target for this family yet) — neither is ever shown as compliant.</summary>
public enum LifecycleCoverageStatus
{
    Unknown,
    NoBaselineSet,
    OnApprovedBaseline,
    BehindBaseline
}
