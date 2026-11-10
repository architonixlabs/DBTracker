using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

public record ScanActivityInput(string InstanceName, string BackupFileLocation, DateTime? LastScanUtc, ScanStatus LastScanStatus, string LastScanError);
public record ScanActivityRow(string InstanceName, string BackupFileLocation, DateTime? LastScanUtc, ScanStatus LastScanStatus, string LastScanError, ScannerHealth Scanner);

/// <summary>
/// Shared-drive scan status across every instance that has one configured — evidence the automated
/// scan is actually running, not just that a path is set. Serves the DBA (is the scanner healthy?)
/// and Auditor (is the file evidence current?) personas. Errored and stale (U02) scans sort first.
/// Pure (no I/O).
/// </summary>
public static class ScanActivityReportBuilder
{
    public static IReadOnlyList<ScanActivityRow> Build(
        IEnumerable<ScanActivityInput> inputs,
        int scanIntervalMinutes,
        int staleAfterMultiplier,
        DateTime nowUtc) =>
        inputs
            .Where(i => !string.IsNullOrWhiteSpace(i.BackupFileLocation))
            .Select(i =>
            {
                var scanner = ScannerStalenessClassifier.Classify(
                    backupFileLocationConfigured: true,
                    lastScanUtc: i.LastScanUtc,
                    lastScanStatus: i.LastScanStatus,
                    scanIntervalMinutes: scanIntervalMinutes,
                    staleAfterMultiplier: staleAfterMultiplier,
                    nowUtc: nowUtc);
                return new ScanActivityRow(i.InstanceName, i.BackupFileLocation, i.LastScanUtc, i.LastScanStatus, i.LastScanError, scanner);
            })
            .OrderByDescending(r => r.Scanner.Freshness is ScannerFreshness.Error or ScannerFreshness.Stale or ScannerFreshness.NeverScanned)
            .ThenBy(r => r.InstanceName)
            .ToList();
}
