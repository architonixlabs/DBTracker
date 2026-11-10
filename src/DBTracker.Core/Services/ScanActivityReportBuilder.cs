using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

public record ScanActivityInput(string InstanceName, string BackupFileLocation, DateTime? LastScanUtc, ScanStatus LastScanStatus, string LastScanError);
public record ScanActivityRow(string InstanceName, string BackupFileLocation, DateTime? LastScanUtc, ScanStatus LastScanStatus, string LastScanError);

/// <summary>
/// Shared-drive scan status across every instance that has one configured — evidence the automated
/// scan is actually running, not just that a path is set. Serves the DBA (is the scanner healthy?)
/// and Auditor (is the file evidence current?) personas. Errored scans sort first. Pure (no I/O).
/// </summary>
public static class ScanActivityReportBuilder
{
    public static IReadOnlyList<ScanActivityRow> Build(IEnumerable<ScanActivityInput> inputs) =>
        inputs
            .Where(i => !string.IsNullOrWhiteSpace(i.BackupFileLocation))
            .Select(i => new ScanActivityRow(i.InstanceName, i.BackupFileLocation, i.LastScanUtc, i.LastScanStatus, i.LastScanError))
            .OrderByDescending(r => r.LastScanStatus == ScanStatus.Error)
            .ThenBy(r => r.InstanceName)
            .ToList();
}
