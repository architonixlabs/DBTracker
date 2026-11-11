namespace DBTracker.Core.Enums;

/// <summary>Classified state of one recorded maintenance result. Kept separate from BackupHealth, and never a claim of continuously measured HA health.</summary>
public enum MaintenanceEvidenceStatus
{
    /// <summary>No schedule to judge staleness against, or no run recorded. Not assumed healthy.</summary>
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
