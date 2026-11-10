using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>
/// D20 — pure key-rotation classification, deliberately a separate evaluator/enum from
/// CertificateExpiryEvaluator: a certificate can be far from expiring while its key is overdue for
/// rotation, and vice versa. Nothing here conflates the two concepts.
/// </summary>
public static class KeyRotationEvaluator
{
    public static KeyRotationStatus Classify(DateTime? lastRotatedAtUtc, int? rotationIntervalDays, DateTime nowUtc)
    {
        if (lastRotatedAtUtc is null || rotationIntervalDays is null)
        {
            return KeyRotationStatus.Unknown;
        }

        return lastRotatedAtUtc.Value.AddDays(rotationIntervalDays.Value) <= nowUtc ? KeyRotationStatus.Overdue : KeyRotationStatus.Current;
    }
}
