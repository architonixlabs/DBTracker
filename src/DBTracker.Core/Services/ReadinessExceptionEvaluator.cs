using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>Rules for readiness exceptions. Expiry is computed from ExpiresAtUtc, so IsActive re-blocks the moment an exception lapses without waiting for a sweep; CanApprove enforces separation of duties when required.</summary>
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

        return nowUtc >= exception.ExpiresAtUtc ? ReadinessExceptionEffectiveStatus.Expired : ReadinessExceptionEffectiveStatus.Active;
    }

    public static bool IsActive(ReadinessException exception, DateTime nowUtc) =>
        EffectiveStatus(exception, nowUtc) == ReadinessExceptionEffectiveStatus.Active;

    /// <summary>True when any exception in the set is currently Active for the target. Every gate bypass check goes through here.</summary>
    public static bool HasActiveExceptionFor(IEnumerable<ReadinessException> exceptions, string targetType, int targetId, DateTime nowUtc) =>
        exceptions.Any(e =>
            string.Equals(e.TargetType, targetType, StringComparison.OrdinalIgnoreCase) &&
            e.TargetId == targetId &&
            IsActive(e, nowUtc));

    public static bool CanApprove(ReadinessException exception, string approverUserId) =>
        !exception.RequiresSeparateApprover ||
        !string.Equals(exception.PreparedByUserId, approverUserId, StringComparison.OrdinalIgnoreCase);
}
