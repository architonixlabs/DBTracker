using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class RetentionCoverageComparerTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

    private static BackupCopyLocation Accessible(DateTime firstDiscoveredUtc, DateTime lastVerifiedUtc) => new()
    {
        VerificationStatus = CopyVerificationStatus.Accessible,
        FirstDiscoveredAtUtc = firstDiscoveredUtc,
        LastVerifiedAtUtc = lastVerifiedUtc
    };

    [Fact]
    public void NoPolicy_IsUnknown()
    {
        var result = RetentionCoverageComparer.Compare(null, [Accessible(Now.AddDays(-100), Now)], Now, 30);
        Assert.Equal(RetentionCoverageStatus.Unknown, result.Status);
    }

    [Fact]
    public void NoCopiesRecorded_IsUnknown()
    {
        var result = RetentionCoverageComparer.Compare(30, [], Now, 30);
        Assert.Equal(RetentionCoverageStatus.Unknown, result.Status);
    }

    [Fact]
    public void OnlyHistoricallyDiscoveredCopy_NeverVerifiedCurrently_IsGap()
    {
        var copy = new BackupCopyLocation { VerificationStatus = CopyVerificationStatus.NeverVerified, FirstDiscoveredAtUtc = Now.AddDays(-400) };

        var result = RetentionCoverageComparer.Compare(90, [copy], Now, 30);

        Assert.Equal(RetentionCoverageStatus.Gap, result.Status);
    }

    [Fact]
    public void AccessibleCopy_DiscoveredLongEnoughAgo_IsCompliant()
    {
        var copy = Accessible(Now.AddDays(-120), Now.AddDays(-1));

        var result = RetentionCoverageComparer.Compare(90, [copy], Now, 30);

        Assert.Equal(RetentionCoverageStatus.Compliant, result.Status);
    }

    [Fact]
    public void AccessibleCopy_DiscoveredTooRecently_IsGap()
    {
        var copy = Accessible(Now.AddDays(-10), Now.AddDays(-1));

        var result = RetentionCoverageComparer.Compare(90, [copy], Now, 30);

        Assert.Equal(RetentionCoverageStatus.Gap, result.Status);
    }

    [Fact]
    public void StaleVerification_DoesNotCountTowardCompliance_EvenIfDiscoveredLongAgo()
    {
        var copy = Accessible(Now.AddDays(-400), Now.AddDays(-60));

        var result = RetentionCoverageComparer.Compare(90, [copy], Now, 30);

        Assert.Equal(RetentionCoverageStatus.Gap, result.Status);
    }
}
