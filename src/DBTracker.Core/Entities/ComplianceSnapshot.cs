namespace DBTracker.Core.Entities;

/// <summary>
/// A persisted, reproducible compliance report (U10). Rows are frozen as JSON at generation time —
/// reopening/exporting a snapshot later must reproduce its original values even if the underlying
/// instances are since edited, archived or backdated. This release only retains already-generated
/// snapshots; it doesn't reconstruct arbitrary historical inventory state.
/// </summary>
public class ComplianceSnapshot
{
    public int Id { get; set; }

    /// <summary>When this snapshot was actually generated (wall-clock).</summary>
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>The point in time the snapshot's data represents — usually equal to GeneratedAtUtc, but can be an explicit custom as-of time.</summary>
    public DateTime EffectiveAsOfUtc { get; set; }

    /// <summary>The actor who generated it. Self-contained for now — a natural candidate to fold into a unified audit log (U19) once that exists.</summary>
    public string GeneratedByUserName { get; set; } = string.Empty;

    /// <summary>Human-readable description of what was included — e.g. "Active + passive, All environments" or "Full inventory".</summary>
    public string ScopeDescription { get; set; } = string.Empty;

    /// <summary>Label for the health/evidence rules in effect when this snapshot was built — a placeholder until a real versioned policy model exists.</summary>
    public string PolicyVersion { get; set; } = "default-v1";

    /// <summary>Where the evidence came from — e.g. which health calculator/report builder produced these rows.</summary>
    public string EvidenceProvenance { get; set; } = string.Empty;

    /// <summary>Frozen rows, serialized via ComplianceSnapshotSerializer. Never recomputed after generation.</summary>
    public string RowsJson { get; set; } = string.Empty;

    public int RowCount { get; set; }
}
