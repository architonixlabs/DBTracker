namespace DBTracker.Core.Enums;

/// <summary>
/// The classified health of one MaintenanceEvidenceRecord (D18) — kept as its own enum/axis,
/// deliberately separate from BackupHealth, so a Failed/Stale integrity check or missed maintenance
/// job is never confused with, or folded into, ordinary backup health (U04's "separate concerns"
/// pattern). This is always a classification of a recorded, point-in-time result — never a claim of
/// continuously measured HA health.
/// </summary>
public enum MaintenanceEvidenceStatus
{
    /// <summary>No schedule recorded to judge staleness against, or no run ever recorded — not assumed healthy.</summary>
    Unknown,

    /// <summary>Never run at all (RanAtUtc is null) despite a schedule existing.</summary>
    Missing,

    /// <summary>Last recorded outcome was a failure.</summary>
    Failed,

    /// <summary>Last recorded outcome passed, but it's older than the expected schedule allows.</summary>
    Stale,

    /// <summary>Last recorded outcome passed and is within the expected schedule.</summary>
    Current
}
