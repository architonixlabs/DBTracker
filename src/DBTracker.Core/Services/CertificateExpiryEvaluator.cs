using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>D20 — pure certificate-expiry classification. Unknown (no expiry recorded) is never conflated with Ok.</summary>
public static class CertificateExpiryEvaluator
{
    public static CertificateExpiryStatus Classify(DateTime? expiresAtUtc, DateTime nowUtc, int warningDays = 30)
    {
        if (expiresAtUtc is null)
        {
            return CertificateExpiryStatus.Unknown;
        }

        var daysRemaining = (expiresAtUtc.Value - nowUtc).TotalDays;
        if (daysRemaining < 0)
        {
            return CertificateExpiryStatus.Expired;
        }

        return daysRemaining <= warningDays ? CertificateExpiryStatus.ExpiringSoon : CertificateExpiryStatus.Ok;
    }
}
