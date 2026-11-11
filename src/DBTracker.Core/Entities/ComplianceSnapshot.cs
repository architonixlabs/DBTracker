namespace DBTracker.Core.Entities;

/// <summary>A persisted compliance report. Rows are frozen as JSON at generation, so later edits, archives or backdated entries never change it.</summary>
public class ComplianceSnapshot
{
    public int Id { get; set; }

    /// <summary>When this snapshot was actually generated (wall-clock).</summary>
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>The point in time the snapshot's data represents — usually equal to GeneratedAtUtc, but can be an explicit custom as-of time.</summary>
    public DateTime EffectiveAsOfUtc { get; set; }

    /// <summary>The user who generated the snapshot.</summary>
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
