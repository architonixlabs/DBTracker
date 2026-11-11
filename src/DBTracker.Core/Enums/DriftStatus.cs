namespace DBTracker.Core.Enums;

/// <summary>Declared version identity compared against an observed import. A failed or denied observation is Unknown, never Drift.</summary>
public enum DriftStatus
{
    /// <summary>No observation exists yet to compare against.</summary>
    NoObservation,
    Match,
    Drift,
    Unknown
}
