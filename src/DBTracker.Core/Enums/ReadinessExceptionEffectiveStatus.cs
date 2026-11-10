namespace DBTracker.Core.Enums;

/// <summary>D14 — the computed, point-in-time status of a ReadinessException (see ReadinessExceptionEvaluator). Only Active exceptions ever bypass a gate.</summary>
public enum ReadinessExceptionEffectiveStatus
{
    PendingApproval,
    Active,
    Expired,
    Revoked,
    Rejected
}
