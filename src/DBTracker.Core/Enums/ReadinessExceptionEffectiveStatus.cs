namespace DBTracker.Core.Enums;

/// <summary>Computed status of a readiness exception at a point in time. Only Active exceptions bypass a gate.</summary>
public enum ReadinessExceptionEffectiveStatus
{
    PendingApproval,
    Active,
    Expired,
    Revoked,
    Rejected
}
