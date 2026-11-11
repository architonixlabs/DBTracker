using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class ScannerStalenessClassifierTests
{
    private static readonly DateTime Now = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void NotConfigured_WhenNoLocation()
    {
        var result = ScannerStalenessClassifier.Classify(false, null, ScanStatus.NeverScanned, 15, 3, Now);
        Assert.Equal(ScannerFreshness.NotConfigured, result.Freshness);
    }

    [Fact]
    public void NeverScanned_WhenNoLastScan()
    {
        var result = ScannerStalenessClassifier.Classify(true, null, ScanStatus.NeverScanned, 15, 3, Now);
        Assert.Equal(ScannerFreshness.NeverScanned, result.Freshness);
    }

    [Fact]
    public void Error_WhenLastScanFailed_RegardlessOfAge()
    {
        var result = ScannerStalenessClassifier.Classify(true, Now.AddMinutes(-1), ScanStatus.Error, 15, 3, Now);
        Assert.Equal(ScannerFreshness.Error, result.Freshness);
    }

    [Fact]
    public void Healthy_WhenRecentSuccess_WithinStaleThreshold()
    {
        // interval 15, multiplier 3 => stale after 45 min
        var result = ScannerStalenessClassifier.Classify(true, Now.AddMinutes(-30), ScanStatus.Success, 15, 3, Now);
        Assert.Equal(ScannerFreshness.Healthy, result.Freshness);
    }

    [Fact]
    public void Stale_WhenSuccessOlderThanThreshold()
    {
        var result = ScannerStalenessClassifier.Classify(true, Now.AddMinutes(-46), ScanStatus.Success, 15, 3, Now);
        Assert.Equal(ScannerFreshness.Stale, result.Freshness);
        Assert.False(string.IsNullOrWhiteSpace(result.RecoveryHint));
    }

    [Fact]
    public void ExactlyAtStaleThreshold_IsNotYetStale()
    {
        var result = ScannerStalenessClassifier.Classify(true, Now.AddMinutes(-45), ScanStatus.Success, 15, 3, Now);
        Assert.Equal(ScannerFreshness.Healthy, result.Freshness);
    }

    [Fact]
    public void NextExpectedCheck_IsLastScanPlusInterval()
    {
        var lastScan = Now.AddMinutes(-10);
        var result = ScannerStalenessClassifier.Classify(true, lastScan, ScanStatus.Success, 15, 3, Now);
        Assert.Equal(lastScan.AddMinutes(15), result.NextExpectedCheckUtc);
    }

    [Fact]
    public void BackgroundScanDisabled_SuccessShowsDisabled_NotHealthyAndNotStale()
    {
        // IntervalMinutes <= 0 means manual-only scanning (same convention as the hosted service).
        // There's no schedule to go stale against, but calling it Healthy would hide that nothing scans by itself.
        var result = ScannerStalenessClassifier.Classify(true, Now.AddDays(-30), ScanStatus.Success, 0, 3, Now);
        Assert.Equal(ScannerFreshness.Disabled, result.Freshness);
        Assert.Null(result.NextExpectedCheckUtc);
        Assert.Contains("Background scanning is off", result.RecoveryHint);
        Assert.False(ScannerStalenessClassifier.NeedsAttention(result.Freshness));
    }

    [Fact]
    public void BackgroundScanDisabled_NeverScanned_StillNeedsAttention()
    {
        var result = ScannerStalenessClassifier.Classify(true, null, ScanStatus.NeverScanned, 0, 3, Now);
        Assert.Equal(ScannerFreshness.NeverScanned, result.Freshness);
        Assert.True(ScannerStalenessClassifier.NeedsAttention(result.Freshness));
    }

    [Fact]
    public void Error_KeepsLastSuccess_FromSeparateField()
    {
        var lastSuccess = Now.AddHours(-30);
        var result = ScannerStalenessClassifier.Classify(true, Now.AddMinutes(-5), ScanStatus.Error, lastSuccess, 15, 3, Now);

        Assert.Equal(ScannerFreshness.Error, result.Freshness);
        Assert.Equal(Now.AddMinutes(-5), result.LastAttemptUtc);
        Assert.Equal(lastSuccess, result.LastSuccessUtc);
        Assert.Contains("30h ago", result.RecoveryHint);
    }

    [Fact]
    public void Error_WithNoSuccessEver_SaysSo()
    {
        var result = ScannerStalenessClassifier.Classify(true, Now.AddMinutes(-5), ScanStatus.Error, null, 15, 3, Now);
        Assert.Null(result.LastSuccessUtc);
        Assert.Contains("No scan of this location has succeeded yet", result.RecoveryHint);
    }

    [Fact]
    public void Stale_IsJudgedFromLastSuccess_NotLastAttempt()
    {
        // A recent successful attempt also writes LastScanSuccessUtc, so a stored older success means the data is inconsistent;
        // the classifier trusts the stored success time.
        var result = ScannerStalenessClassifier.Classify(true, Now.AddMinutes(-5), ScanStatus.Success, Now.AddHours(-2), 15, 3, Now);
        Assert.Equal(ScannerFreshness.Stale, result.Freshness);
    }

    [Fact]
    public void LegacyRow_WithoutStoredSuccess_UsesSuccessfulLastAttempt()
    {
        var lastScan = Now.AddMinutes(-10);
        var result = ScannerStalenessClassifier.Classify(true, lastScan, ScanStatus.Success, null, 15, 3, Now);
        Assert.Equal(ScannerFreshness.Healthy, result.Freshness);
        Assert.Equal(lastScan, result.LastSuccessUtc);
    }

    [Theory]
    [InlineData(ScannerFreshness.Error, true)]
    [InlineData(ScannerFreshness.Stale, true)]
    [InlineData(ScannerFreshness.NeverScanned, true)]
    [InlineData(ScannerFreshness.Healthy, false)]
    [InlineData(ScannerFreshness.Disabled, false)]
    [InlineData(ScannerFreshness.NotConfigured, false)]
    public void NeedsAttention_MatchesBannerStates(ScannerFreshness freshness, bool expected) =>
        Assert.Equal(expected, ScannerStalenessClassifier.NeedsAttention(freshness));

    [Fact]
    public void BackgroundScanDisabled_ErrorStatusStillReportsError()
    {
        var result = ScannerStalenessClassifier.Classify(true, Now.AddMinutes(-1), ScanStatus.Error, 0, 3, Now);
        Assert.Equal(ScannerFreshness.Error, result.Freshness);
    }

    [Fact]
    public void ErrorScan_StillReportsNextExpectedCheck_SoRecoveryCanBeTimed()
    {
        var lastScan = Now.AddMinutes(-10);
        var result = ScannerStalenessClassifier.Classify(true, lastScan, ScanStatus.Error, 15, 3, Now);
        Assert.Equal(lastScan.AddMinutes(15), result.NextExpectedCheckUtc);
    }
}
