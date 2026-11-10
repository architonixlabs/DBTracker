namespace DBTracker.Core.Enums;

/// <summary>
/// Outcome of one version-metadata observation attempt (D06). A PermissionDenied/Unreachable
/// outcome must never overwrite the last-good declared VersionIdentity and must never mark the
/// instance retired — it only records that the attempt failed.
/// </summary>
public enum ObservationOutcome
{
    Success,
    PermissionDenied,
    Unreachable
}
