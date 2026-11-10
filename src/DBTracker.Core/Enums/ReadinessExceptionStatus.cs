namespace DBTracker.Core.Enums;

/// <summary>
/// D14 — the stored decision state of a ReadinessException. "Expired" is deliberately NOT a stored
/// value here: expiry is computed live from ExpiresAtUtc vs. now (ReadinessExceptionEvaluator), the
/// same pattern D09 already uses for evidence expiry, so nothing has to run a sweep for the gating
/// logic itself to correctly re-block an action the instant an approved exception's clock runs out.
/// </summary>
public enum ReadinessExceptionStatus
{
    PendingApproval,
    Approved,
    Rejected,
    Revoked
}
