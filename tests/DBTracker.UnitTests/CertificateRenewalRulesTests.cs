using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class CertificateRenewalRulesTests
{
    private static readonly DateTime Now = new(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Renewal_ToALaterFutureExpiry_IsAccepted()
    {
        Assert.Null(CertificateRenewalRules.ValidateRenewal(Now.AddDays(5), Now.AddDays(400), Now.AddHours(-1), Now));
    }

    [Fact]
    public void Renewal_WithoutNewExpiry_IsRefused()
    {
        Assert.NotNull(CertificateRenewalRules.ValidateRenewal(Now.AddDays(5), null, Now, Now));
    }

    [Fact]
    public void Renewal_ToAnEarlierOrPastExpiry_IsRefused()
    {
        Assert.NotNull(CertificateRenewalRules.ValidateRenewal(Now.AddDays(50), Now.AddDays(40), Now, Now));
        Assert.NotNull(CertificateRenewalRules.ValidateRenewal(null, Now.AddDays(-1), Now, Now));
    }

    [Fact]
    public void Renewal_DatedInTheFuture_IsRefused()
    {
        Assert.NotNull(CertificateRenewalRules.ValidateRenewal(null, Now.AddDays(300), Now.AddDays(2), Now));
    }

    [Fact]
    public void Rotation_InTheFuture_IsRefused_PastIsAccepted()
    {
        Assert.NotNull(CertificateRenewalRules.ValidateRotation(Now.AddDays(1), Now));
        Assert.Null(CertificateRenewalRules.ValidateRotation(Now.AddDays(-1), Now));
    }
}
