using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class CertificateExpiryEvaluatorTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void NoExpiryRecorded_IsUnknown()
    {
        Assert.Equal(CertificateExpiryStatus.Unknown, CertificateExpiryEvaluator.Classify(null, Now));
    }

    [Fact]
    public void FarInFuture_IsOk()
    {
        Assert.Equal(CertificateExpiryStatus.Ok, CertificateExpiryEvaluator.Classify(Now.AddDays(90), Now, warningDays: 30));
    }

    [Fact]
    public void WithinWarningWindow_IsExpiringSoon()
    {
        Assert.Equal(CertificateExpiryStatus.ExpiringSoon, CertificateExpiryEvaluator.Classify(Now.AddDays(10), Now, warningDays: 30));
    }

    [Fact]
    public void InThePast_IsExpired()
    {
        Assert.Equal(CertificateExpiryStatus.Expired, CertificateExpiryEvaluator.Classify(Now.AddDays(-1), Now, warningDays: 30));
    }

    [Fact]
    public void ExactlyAtWarningThreshold_IsExpiringSoon()
    {
        Assert.Equal(CertificateExpiryStatus.ExpiringSoon, CertificateExpiryEvaluator.Classify(Now.AddDays(30), Now, warningDays: 30));
    }
}

public class KeyRotationEvaluatorTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void NoRotationDataRecorded_IsUnknown()
    {
        Assert.Equal(KeyRotationStatus.Unknown, KeyRotationEvaluator.Classify(null, null, Now));
        Assert.Equal(KeyRotationStatus.Unknown, KeyRotationEvaluator.Classify(Now.AddDays(-10), null, Now));
        Assert.Equal(KeyRotationStatus.Unknown, KeyRotationEvaluator.Classify(null, 90, Now));
    }

    [Fact]
    public void RotatedRecently_IsCurrent()
    {
        Assert.Equal(KeyRotationStatus.Current, KeyRotationEvaluator.Classify(Now.AddDays(-10), 90, Now));
    }

    [Fact]
    public void RotatedLongAgo_IsOverdue()
    {
        Assert.Equal(KeyRotationStatus.Overdue, KeyRotationEvaluator.Classify(Now.AddDays(-100), 90, Now));
    }

    [Fact]
    public void RotationStatus_IsIndependentOfCertificateExpiry()
    {
        // A certificate that's Ok on expiry can simultaneously be Overdue on rotation, and vice versa —
        // the two evaluators never share state or influence each other's result.
        var expiry = CertificateExpiryEvaluator.Classify(Now.AddYears(2), Now);
        var rotation = KeyRotationEvaluator.Classify(Now.AddDays(-400), 90, Now);

        Assert.Equal(CertificateExpiryStatus.Ok, expiry);
        Assert.Equal(KeyRotationStatus.Overdue, rotation);
    }
}
