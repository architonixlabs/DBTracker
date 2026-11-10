using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

public enum ScannerFreshness
{
    /// <summary>Instance has no backup file location configured — scanning doesn't apply.</summary>
    NotConfigured,
    NeverScanned,
    Healthy,
    /// <summary>Last scan succeeded, but long enough ago that monitoring itself may have stopped running.</summary>
    Stale,
    Error
}

public record ScannerHealth(
    ScannerFreshness Freshness,
    DateTime? LastAttemptUtc,
    DateTime? LastSuccessUtc,
    DateTime? NextExpectedCheckUtc,
    string RecoveryHint);

/// <summary>
/// Separates "is the scanner itself still running" (U02) from "is the backup healthy" — an old
/// successful scan can go stale even though its last result was Success, and a stale/erroring
/// scanner must never be read as proof a backup is fine. Pure (no I/O).
/// </summary>
public static class ScannerStalenessClassifier
{
    public static ScannerHealth Classify(
        bool backupFileLocationConfigured,
        DateTime? lastScanUtc,
        ScanStatus lastScanStatus,
        int scanIntervalMinutes,
        int staleAfterMultiplier,
        DateTime nowUtc)
    {
        if (!backupFileLocationConfigured)
        {
            return new ScannerHealth(ScannerFreshness.NotConfigured, lastScanUtc, null, null,
                "No backup file location configured — nothing to scan.");
        }

        if (lastScanUtc is null || lastScanStatus == ScanStatus.NeverScanned)
        {
            return new ScannerHealth(ScannerFreshness.NeverScanned, null, null, null,
                "This instance has never been scanned. Use \"Scan now\" or wait for the next background sweep.");
        }

        // scanIntervalMinutes <= 0 means the periodic background sweep is disabled (manual "Scan
        // now" only, same convention as BackupScanHostedService) — there's no schedule to have
        // gone stale against, so only Error/NeverScanned/Healthy apply, never Stale.
        if (scanIntervalMinutes <= 0)
        {
            return lastScanStatus == ScanStatus.Error
                ? new ScannerHealth(ScannerFreshness.Error, lastScanUtc, null, null,
                    "The last scan attempt failed. Check the path is reachable, then retry with \"Scan now\".")
                : new ScannerHealth(ScannerFreshness.Healthy, lastScanUtc, lastScanUtc, null, "");
        }

        var interval = TimeSpan.FromMinutes(scanIntervalMinutes);
        var nextExpected = lastScanUtc.Value + interval;

        if (lastScanStatus == ScanStatus.Error)
        {
            return new ScannerHealth(ScannerFreshness.Error, lastScanUtc, null, nextExpected,
                "The last scan attempt failed. Check the path is reachable, then retry with \"Scan now\".");
        }

        var staleAfter = TimeSpan.FromMinutes(scanIntervalMinutes * Math.Max(1, staleAfterMultiplier));
        var isStale = nowUtc - lastScanUtc.Value > staleAfter;

        return isStale
            ? new ScannerHealth(ScannerFreshness.Stale, lastScanUtc, lastScanUtc, nextExpected,
                $"No successful scan in over {staleAfter.TotalHours:N0}h — the background scanner may have stopped. Check it's still running, or scan now.")
            : new ScannerHealth(ScannerFreshness.Healthy, lastScanUtc, lastScanUtc, nextExpected, "");
    }
}
