using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>
/// Pure (no I/O) rules for D14's generic exception/waiver. EffectiveStatus computes Expired live
/// from Status + ExpiresAtUtc vs. "now" — nothing mutates ReadinessException.Status to Expired on
/// its own, so a gate that checks IsActive always re-blocks the instant the clock runs out, without
/// depending on a sweep having already run. CanApprove enforces separation of duties when the
/// exception's policy requires it.
/// </summary>
public static class ReadinessExceptionEvaluator
{
    public static ReadinessExceptionEffectiveStatus EffectiveStatus(ReadinessException exception, DateTime nowUtc)
    {
        if (exception.Status == ReadinessExceptionStatus.Revoked)
        {
            return ReadinessExceptionEffectiveStatus.Revoked;
        }
        if (exception.Status == ReadinessExceptionStatus.Rejected)
        {
            return ReadinessExceptionEffectiveStatus.Rejected;
        }
        if (exception.Status == ReadinessExceptionStatus.PendingApproval)
        {
            return ReadinessExceptionEffectiveStatus.PendingApproval;
        }

        // Approved:
        return nowUtc >= exception.ExpiresAtUtc ? ReadinessExceptionEffectiveStatus.Expired : ReadinessExceptionEffectiveStatus.Active;
    }

    public static bool IsActive(ReadinessException exception, DateTime nowUtc) =>
        EffectiveStatus(exception, nowUtc) == ReadinessExceptionEffectiveStatus.Active;

    /// <summary>True when any exception in the set is currently Active for the given target — the single place every D05/D09/D11/D12/D13 bypass check should call through.</summary>
    public static bool HasActiveExceptionFor(IEnumerable<ReadinessException> exceptions, string targetType, int targetId, DateTime nowUtc) =>
        exceptions.Any(e =>
            string.Equals(e.TargetType, targetType, StringComparison.OrdinalIgnoreCase) &&
            e.TargetId == targetId &&
            IsActive(e, nowUtc));

    public static bool CanApprove(ReadinessException exception, string approverUserId) =>
        !exception.RequiresSeparateApprover ||
        !string.Equals(exception.PreparedByUserId, approverUserId, StringComparison.OrdinalIgnoreCase);
}
