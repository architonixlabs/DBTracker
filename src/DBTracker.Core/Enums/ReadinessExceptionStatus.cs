namespace DBTracker.Core.Enums;

/// <summary>Stored decision state of a readiness exception. Expired is computed from ExpiresAtUtc, not stored, so a gate re-blocks the moment an exception lapses without waiting for a sweep.</summary>
public enum ReadinessExceptionStatus
{
    PendingApproval,
    Approved,
    Rejected,
    Revoked
}
