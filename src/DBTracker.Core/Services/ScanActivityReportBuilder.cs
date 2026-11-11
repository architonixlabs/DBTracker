using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

public record ScanActivityInput(string InstanceName, string BackupFileLocation, DateTime? LastScanUtc, ScanStatus LastScanStatus, string LastScanError, DateTime? LastScanSuccessUtc = null, int InstanceId = 0);
public record ScanActivityRow(string InstanceName, string BackupFileLocation, DateTime? LastScanUtc, ScanStatus LastScanStatus, string LastScanError, ScannerHealth Scanner, int InstanceId = 0);

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
                    lastAttemptUtc: i.LastScanUtc,
                    lastScanStatus: i.LastScanStatus,
                    lastSuccessUtc: i.LastScanSuccessUtc,
                    scanIntervalMinutes: scanIntervalMinutes,
                    staleAfterMultiplier: staleAfterMultiplier,
                    nowUtc: nowUtc);
                return new ScanActivityRow(i.InstanceName, i.BackupFileLocation, i.LastScanUtc, i.LastScanStatus, i.LastScanError, scanner, i.InstanceId);
            })
            .OrderByDescending(r => ScannerStalenessClassifier.NeedsAttention(r.Scanner.Freshness))
            .ThenBy(r => r.InstanceName)
            .ToList();

    /// <summary>Only the rows the dashboard banner counts, so its link lands on exactly those rows.</summary>
    public static IReadOnlyList<ScanActivityRow> NeedingAttention(IEnumerable<ScanActivityRow> rows) =>
        rows.Where(r => ScannerStalenessClassifier.NeedsAttention(r.Scanner.Freshness)).ToList();
}
