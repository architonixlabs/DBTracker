using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class BackupCopyAvailabilityEvaluatorTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void NeverVerified_ButDiscoveredLongAgo_IsNotCurrentlyAvailable()
    {
        var copy = new BackupCopyLocation { VerificationStatus = CopyVerificationStatus.NeverVerified, FirstDiscoveredAtUtc = Now.AddYears(-1) };

        var result = BackupCopyAvailabilityEvaluator.Evaluate(copy, Now, 30);

        Assert.False(result.IsCurrentlyVerifiedAvailable);
        Assert.Contains("historical discovery", result.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AccessibleAndRecentlyVerified_IsCurrentlyAvailable()
    {
        var copy = new BackupCopyLocation { VerificationStatus = CopyVerificationStatus.Accessible, LastVerifiedAtUtc = Now.AddDays(-1) };

        var result = BackupCopyAvailabilityEvaluator.Evaluate(copy, Now, 30);

        Assert.True(result.IsCurrentlyVerifiedAvailable);
        Assert.False(result.HasStaleVerification);
    }

    [Fact]
    public void AccessibleButStaleVerification_IsNotCurrentlyAvailable()
    {
        var copy = new BackupCopyLocation { VerificationStatus = CopyVerificationStatus.Accessible, LastVerifiedAtUtc = Now.AddDays(-45) };

        var result = BackupCopyAvailabilityEvaluator.Evaluate(copy, Now, 30);

        Assert.False(result.IsCurrentlyVerifiedAvailable);
        Assert.True(result.HasStaleVerification);
    }

    [Fact]
    public void CannotVerify_IsDistinctFromMissing()
    {
        var cannotVerify = new BackupCopyLocation { VerificationStatus = CopyVerificationStatus.CannotVerify, LastVerifiedAtUtc = Now };
        var missing = new BackupCopyLocation { VerificationStatus = CopyVerificationStatus.Missing, LastVerifiedAtUtc = Now };

        var cannotVerifyResult = BackupCopyAvailabilityEvaluator.Evaluate(cannotVerify, Now, 30);
        var missingResult = BackupCopyAvailabilityEvaluator.Evaluate(missing, Now, 30);

        Assert.False(cannotVerifyResult.IsCurrentlyVerifiedAvailable);
        Assert.False(missingResult.IsCurrentlyVerifiedAvailable);
        Assert.NotEqual(cannotVerifyResult.Explanation, missingResult.Explanation);
    }

    [Fact]
    public void NeverVerified_WithNoDiscoveryDate_DistinctExplanationFromDiscoveredCase()
    {
        var copy = new BackupCopyLocation { VerificationStatus = CopyVerificationStatus.NeverVerified };

        var result = BackupCopyAvailabilityEvaluator.Evaluate(copy, Now, 30);

        Assert.False(result.IsCurrentlyVerifiedAvailable);
        Assert.Equal("Never verified.", result.Explanation);
    }
}
