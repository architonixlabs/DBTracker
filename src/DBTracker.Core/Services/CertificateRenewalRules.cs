namespace DBTracker.Core.Services;

/// <summary>Checks on a recorded certificate renewal or key rotation. Renewal moves the expiry date; rotation replaces key material and is
/// judged separately by KeyRotationEvaluator, so recording one never implies the other.</summary>
public static class CertificateRenewalRules
{
    private static readonly TimeSpan FutureTolerance = TimeSpan.FromMinutes(5);

    public static string? ValidateRenewal(DateTime? previousExpiresAtUtc, DateTime? newExpiresAtUtc, DateTime renewedAtUtc, DateTime nowUtc)
    {
        if (renewedAtUtc > nowUtc + FutureTolerance)
        {
            return "The renewal date can't be in the future. Record it once the new certificate is in place.";
        }
        if (newExpiresAtUtc is not { } newExpiry)
        {
            return "Enter the new certificate's expiry date.";
        }
        if (newExpiry <= nowUtc)
        {
            return "The new expiry date must be in the future.";
        }
        if (previousExpiresAtUtc is { } previous && newExpiry <= previous)
        {
            return $"The new expiry date must be later than the current one ({previous:yyyy-MM-dd}).";
        }
        return null;
    }

    public static string? ValidateRotation(DateTime rotatedAtUtc, DateTime nowUtc) =>
        rotatedAtUtc > nowUtc + FutureTolerance ? "The rotation date can't be in the future." : null;
}
