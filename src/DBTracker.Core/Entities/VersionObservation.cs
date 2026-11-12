using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>One version-metadata observation (manual entry or import). A failed attempt is recorded but never overwrites the declared identity or retires the instance.</summary>
public class VersionObservation
{
    public int Id { get; set; }

    public int DatabaseInstanceId { get; set; }
    public DatabaseInstance? DatabaseInstance { get; set; }

    public VersionObservationSource Source { get; set; } = VersionObservationSource.ManualEntry;
    public string RawVersionText { get; set; } = string.Empty;

    /// <summary>Edition and database compatibility level as observed, compared with the declared identity when recorded.</summary>
    public string ObservedEdition { get; set; } = string.Empty;
    public string ObservedCompatibilityLevel { get; set; } = string.Empty;

    public ObservationOutcome Outcome { get; set; } = ObservationOutcome.Success;
    public string FailureDetail { get; set; } = string.Empty;

    public DateTime ObservedAtUtc { get; set; } = DateTime.UtcNow;
    public string ObservedBy { get; set; } = string.Empty;

    public DriftStatus DriftStatus { get; set; } = DriftStatus.NoObservation;

    /// <summary>Comma-separated field names that differed from the declared VersionIdentity at observation time, when DriftStatus is Drift.</summary>
    public string DriftDetail { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;
}
