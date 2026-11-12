using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

public enum ScannerFreshness
{
    /// <summary>Instance has no backup file location configured, so scanning doesn't apply.</summary>
    NotConfigured,
    NeverScanned,
    Healthy,
    /// <summary>Last scan succeeded, but long enough ago that monitoring itself may have stopped running.</summary>
    Stale,
    Error,
    /// <summary>The background sweep is off (BackupScan:IntervalMinutes &lt;= 0). Only a manual "Scan now" updates the instance, so it can't go stale.</summary>
    Disabled
}

public record ScannerHealth(
    ScannerFreshness Freshness,
    DateTime? LastAttemptUtc,
    DateTime? LastSuccessUtc,
    DateTime? NextExpectedCheckUtc,
    string RecoveryHint);

/// <summary>Whether the scanner itself is still running, separate from backup health. A stale or erroring scanner is never proof a backup is fine.</summary>
public static class ScannerStalenessClassifier
{
    /// <summary>For callers that only know the last attempt. A successful last attempt counts as the last success.</summary>
    public static ScannerHealth Classify(
        bool backupFileLocationConfigured,
        DateTime? lastScanUtc,
        ScanStatus lastScanStatus,
        int scanIntervalMinutes,
        int staleAfterMultiplier,
        DateTime nowUtc) =>
        Classify(backupFileLocationConfigured, lastScanUtc, lastScanStatus, null,
            scanIntervalMinutes, staleAfterMultiplier, nowUtc);

    /// <param name="lastAttemptUtc">When the last scan ran, whatever its result.</param>
    /// <param name="lastSuccessUtc">When a scan last succeeded, stored apart from the attempt so a failed scan doesn't erase it.
    /// When null and the last attempt succeeded (rows written before it was stored), the last attempt counts.</param>
    public static ScannerHealth Classify(
        bool backupFileLocationConfigured,
        DateTime? lastAttemptUtc,
        ScanStatus lastScanStatus,
        DateTime? lastSuccessUtc,
        int scanIntervalMinutes,
        int staleAfterMultiplier,
        DateTime nowUtc)
    {
        if (lastSuccessUtc is null && lastScanStatus == ScanStatus.Success)
        {
            lastSuccessUtc = lastAttemptUtc;
        }

        if (!backupFileLocationConfigured)
        {
            return new ScannerHealth(ScannerFreshness.NotConfigured, lastAttemptUtc, lastSuccessUtc, null,
                "No backup file location configured, so there's nothing to scan.");
        }

        var backgroundOff = scanIntervalMinutes <= 0;

        if (lastAttemptUtc is null || lastScanStatus == ScanStatus.NeverScanned)
        {
            return new ScannerHealth(ScannerFreshness.NeverScanned, null, lastSuccessUtc, null,
                backgroundOff
                    ? "This instance has never been scanned, and background scanning is off (BackupScan:IntervalMinutes is 0). Use \"Scan now\"."
                    : "This instance has never been scanned. Use \"Scan now\" or wait for the next background sweep.");
        }

        if (lastScanStatus == ScanStatus.Error)
        {
            var since = lastSuccessUtc is { } ok
                ? $" The last successful scan was {DescribeAge(nowUtc - ok)} ago."
                : " No scan of this location has succeeded yet.";
            return new ScannerHealth(ScannerFreshness.Error, lastAttemptUtc, lastSuccessUtc,
                backgroundOff ? null : lastAttemptUtc.Value + TimeSpan.FromMinutes(scanIntervalMinutes),
                "The last scan attempt failed. Check the path is reachable from the server, then retry with \"Scan now\"." + since +
                (backgroundOff ? " Background scanning is off, so it won't retry by itself." : ""));
        }

        if (backgroundOff)
        {
            return new ScannerHealth(ScannerFreshness.Disabled, lastAttemptUtc, lastSuccessUtc, null,
                "Background scanning is off (BackupScan:IntervalMinutes is 0). Only \"Scan now\" updates this instance, so new backup files aren't picked up by themselves.");
        }

        var interval = TimeSpan.FromMinutes(scanIntervalMinutes);
        var nextExpected = lastAttemptUtc.Value + interval;
        var staleAfter = TimeSpan.FromMinutes(scanIntervalMinutes * Math.Max(1, staleAfterMultiplier));
        var reference = lastSuccessUtc ?? lastAttemptUtc.Value;
        var isStale = nowUtc - reference > staleAfter;

        return isStale
            ? new ScannerHealth(ScannerFreshness.Stale, lastAttemptUtc, lastSuccessUtc, nextExpected,
                $"No successful scan in over {DescribeAge(staleAfter)}. The background scanner may have stopped. Check the scan worker on Setup and diagnostics, or scan now.")
            : new ScannerHealth(ScannerFreshness.Healthy, lastAttemptUtc, lastSuccessUtc, nextExpected, "");
    }

    /// <summary>The states the dashboard banner counts and the Scan activity "needs attention" filter shows.</summary>
    public static bool NeedsAttention(ScannerFreshness freshness) =>
        freshness is ScannerFreshness.Error or ScannerFreshness.Stale or ScannerFreshness.NeverScanned;

    public static string Describe(ScannerFreshness freshness) => freshness switch
    {
        ScannerFreshness.NotConfigured => "Not configured",
        ScannerFreshness.NeverScanned => "Never scanned",
        ScannerFreshness.Disabled => "Background scan off",
        _ => freshness.ToString()
    };

    private static string DescribeAge(TimeSpan age) =>
        age.TotalHours >= 48 ? $"{age.TotalDays:N0} days"
        : age.TotalHours >= 1 ? $"{age.TotalHours:N0}h"
        : $"{Math.Max(1, age.TotalMinutes):N0} min";
}
