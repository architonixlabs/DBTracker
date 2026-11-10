using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

public record BackupActivityRow(
    string InstanceName,
    string Company,
    string Department,
    DatabaseEngine Engine,
    DateTime TimestampUtc,
    BackupType Type,
    BackupStatus Status,
    double? SizeMb,
    double? DurationMinutes,
    BackupSource Source);

public record BackupActivitySummary(int TotalRuns, int SuccessCount, int FailedCount, int MissedCount, double TotalSizeMb, double? AvgDurationMinutes);

/// <summary>
/// Backup runs within a date window — what actually happened, not just current state. Serves the
/// DBA persona: spotting patterns (a flaky job, growing backup sizes, duration creep) that a
/// point-in-time snapshot can't show. Pure (no I/O), so it's unit-testable without a database.
/// </summary>
public static class BackupActivityReportBuilder
{
    public static IReadOnlyList<BackupActivityRow> InRange(IEnumerable<BackupActivityRow> rows, DateTime fromUtc, DateTime toUtc) =>
        rows
            .Where(r => r.TimestampUtc >= fromUtc && r.TimestampUtc <= toUtc)
            .OrderByDescending(r => r.TimestampUtc)
            .ToList();

    public static BackupActivitySummary Summarize(IReadOnlyCollection<BackupActivityRow> rowsInRange)
    {
        var withDuration = rowsInRange.Where(r => r.DurationMinutes is not null).ToList();

        return new BackupActivitySummary(
            TotalRuns: rowsInRange.Count,
            SuccessCount: rowsInRange.Count(r => r.Status == BackupStatus.Success),
            FailedCount: rowsInRange.Count(r => r.Status == BackupStatus.Failed),
            MissedCount: rowsInRange.Count(r => r.Status == BackupStatus.Missed),
            TotalSizeMb: rowsInRange.Sum(r => r.SizeMb ?? 0),
            AvgDurationMinutes: withDuration.Count > 0 ? withDuration.Average(r => r.DurationMinutes!.Value) : null);
    }
}
