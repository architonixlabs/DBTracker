using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

public record ScanActivityInput(string InstanceName, string BackupFileLocation, DateTime? LastScanUtc, ScanStatus LastScanStatus, string LastScanError);
public record ScanActivityRow(string InstanceName, string BackupFileLocation, DateTime? LastScanUtc, ScanStatus LastScanStatus, string LastScanError, ScannerHealth Scanner);

/// <summary>Shared-drive scan status for every instance with a location configured, as evidence the scanner is actually running. Errored and stale scans sort first.</summary>
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
