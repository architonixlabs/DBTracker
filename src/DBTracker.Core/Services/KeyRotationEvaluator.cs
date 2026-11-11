using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>Key-rotation classification, separate from certificate expiry: either can be due without the other.</summary>
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
