namespace DBTracker.Core.Enums;

/// <summary>
/// Result of comparing a declared version identity (D01, manually attested/entered) against an
/// observed metadata import (D06). A failed/denied observation is Unknown, never Drift — a
/// permission failure must never be reported as evidence the asset changed.
/// </summary>
public enum DriftStatus
{
    /// <summary>No observation exists yet to compare against.</summary>
    NoObservation,
    Match,
    Drift,
    Unknown
}
