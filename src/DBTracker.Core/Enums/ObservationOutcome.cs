namespace DBTracker.Core.Enums;

/// <summary>Outcome of a version-metadata observation. PermissionDenied or Unreachable never overwrites the declared identity or retires the instance.</summary>
public enum ObservationOutcome
{
    Success,
    PermissionDenied,
    Unreachable
}
