using System.Text;
using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

public record ComplianceReportRow(DatabaseInstance Instance, BackupHealth Health, DateTime? LastBackupUtc, bool RestoreTested);

/// <summary>
/// Builds the plain-text body of the scheduled/on-demand management report — a leadership-readable
/// roll-up of backup health and restore-test coverage, reusing the same data the Compliance view
/// shows. Pure (no I/O), so it's unit-testable without a database.
/// </summary>
public static class ComplianceReportBuilder
{
    public static string BuildPlainTextBody(IReadOnlyCollection<ComplianceReportRow> rows, DateTime asOfUtc)
    {
        var sb = new StringBuilder();
        var total = rows.Count;
        var failed = rows.Count(r => r.Health == BackupHealth.Failed);
        var overdue = rows.Count(r => r.Health == BackupHealth.Overdue);
        var onTrack = rows.Count(r => r.Health == BackupHealth.OnTrack);
        var never = rows.Count(r => r.Health == BackupHealth.NeverBackedUp);
        var restoreTested = rows.Count(r => r.RestoreTested);

        sb.AppendLine($"DBTracker backup compliance report — as of {asOfUtc:yyyy-MM-dd HH:mm} UTC");
        sb.AppendLine(new string('-', 60));
        sb.AppendLine($"Total instances tracked: {total}");
        sb.AppendLine($"  On track:          {onTrack}");
        sb.AppendLine($"  Overdue:           {overdue}");
        sb.AppendLine($"  Failed:            {failed}");
        sb.AppendLine($"  Never backed up:   {never}");
        sb.AppendLine($"  Restore tested:    {restoreTested} of {total}");
        sb.AppendLine();

        var atRisk = rows
            .Where(r => r.Health is BackupHealth.Failed or BackupHealth.Overdue or BackupHealth.NeverBackedUp)
            .OrderBy(r => r.Health switch { BackupHealth.Failed => 0, BackupHealth.NeverBackedUp => 1, _ => 2 })
            .ThenBy(r => r.Instance.Name)
            .ToList();

        if (atRisk.Count == 0)
        {
            sb.AppendLine("No instances currently at risk — every tracked instance is on track.");
        }
        else
        {
            sb.AppendLine("Instances needing attention:");
            foreach (var row in atRisk)
            {
                var lastBackup = row.LastBackupUtc is null ? "never" : row.LastBackupUtc.Value.ToString("yyyy-MM-dd HH:mm") + " UTC";
                sb.AppendLine($"  [{row.Health,-14}] {row.Instance.Name} ({row.Instance.Company}/{row.Instance.Department}) — last backup: {lastBackup}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("Generated automatically by DBTracker. Open DBTracker's Compliance view for the full, filterable detail.");

        return sb.ToString();
    }

    public static string BuildSubject(IReadOnlyCollection<ComplianceReportRow> rows, DateTime asOfUtc)
    {
        var atRisk = rows.Count(r => r.Health is BackupHealth.Failed or BackupHealth.Overdue or BackupHealth.NeverBackedUp);
        return atRisk == 0
            ? $"DBTracker compliance report {asOfUtc:yyyy-MM-dd} — all clear"
            : $"DBTracker compliance report {asOfUtc:yyyy-MM-dd} — {atRisk} instance(s) need attention";
    }
}
