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
    public void BackgroundScanDisabled_OldSuccessIsStillHealthy_NeverStale()
    {
        // IntervalMinutes <= 0 means manual-only scanning (same convention as the hosted service) —
        // there's no periodic schedule to have gone stale against.
        var result = ScannerStalenessClassifier.Classify(true, Now.AddDays(-30), ScanStatus.Success, 0, 3, Now);
        Assert.Equal(ScannerFreshness.Healthy, result.Freshness);
        Assert.Null(result.NextExpectedCheckUtc);
    }

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
