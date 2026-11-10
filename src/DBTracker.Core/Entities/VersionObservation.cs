using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>
/// One version-metadata observation attempt (D06) — manual entry/import today; FutureCollector is a
/// labelled extension point only, not implemented. A failed attempt (PermissionDenied/Unreachable)
/// is recorded honestly and never overwrites VersionIdentity (the declared/attested value) and never
/// marks the instance retired. DriftStatus/DriftDetail compare this observation against the
/// instance's current declared VersionIdentity at the time it was recorded.
/// </summary>
public class VersionObservation
{
    public int Id { get; set; }

    public int DatabaseInstanceId { get; set; }
    public DatabaseInstance? DatabaseInstance { get; set; }

    public VersionObservationSource Source { get; set; } = VersionObservationSource.ManualEntry;
    public string RawVersionText { get; set; } = string.Empty;

    public ObservationOutcome Outcome { get; set; } = ObservationOutcome.Success;
    public string FailureDetail { get; set; } = string.Empty;

    public DateTime ObservedAtUtc { get; set; } = DateTime.UtcNow;
    public string ObservedBy { get; set; } = string.Empty;

    public DriftStatus DriftStatus { get; set; } = DriftStatus.NoObservation;

    /// <summary>Comma-separated field names that differed from the declared VersionIdentity at observation time, when DriftStatus is Drift.</summary>
    public string DriftDetail { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;
}
