using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>Minimal backup-event facts needed for a trend period — raw, dated events, not a recomputed health status.</summary>
public record TrendBackupEvent(string InstanceName, DateTime TimestampUtc, BackupStatus Status, double? SizeMb, double? DurationMinutes);

/// <summary>An incident open as of the period's end — the actual persisted U08 timestamp, not a status recomputed from today's policy.</summary>
public record TrendOutstandingRisk(string InstanceName, DateTime OpenedAtUtc, BackupHealth TriggerHealth);

public record RepeatedFailureRow(string InstanceName, int FailureCount);

/// <summary>
/// Everything one side of a trend comparison needs (U22). SnapshotRows/SnapshotGeneratedAtUtc come
/// from the nearest U10 ComplianceSnapshot at or before ToUtc — null means no snapshot covers this
/// period, which the builder reports as an explicit missing-evidence period rather than silently
/// falling back to today's live inventory (which would misrepresent instances that didn't exist yet
/// or whose policy has since changed). BackupEvents/RestoreTestTimestamps/OutstandingRisks are raw,
/// dated facts (BackupRecord/RestoreEvent/Incident rows already filtered to this period by the
/// caller) — counting them isn't "inferring past health", it's counting things that actually
/// happened at a known time.
/// </summary>
public record TrendPeriodInput(
    DateTime FromUtc,
    DateTime ToUtc,
    IReadOnlyList<ComplianceSnapshotRow>? SnapshotRows,
    DateTime? SnapshotGeneratedAtUtc,
    IReadOnlyList<TrendBackupEvent> BackupEvents,
    IReadOnlyList<DateTime> RestoreTestTimestamps,
    IReadOnlyList<TrendOutstandingRisk> OutstandingRisks);

public record TrendPeriodResult(
    DateTime FromUtc,
    DateTime ToUtc,
    bool MissingEvidence,
    DateTime? SnapshotGeneratedAtUtc,
    int? InventoryCount,
    int? OnTrackCount,
    int? AtRiskCount,
    double? OnTrackPct,
    IReadOnlyList<ComplianceSnapshotRow> AtRiskRows,
    int? RestoreTestedCount,
    double? RestoreTestedPct,
    int RepeatedFailureInstanceCount,
    IReadOnlyList<RepeatedFailureRow> RepeatedFailureRows,
    int BackupEventCount,
    double? AvgBackupDurationMinutes,
    double? AvgBackupSizeMb,
    double? AverageOutstandingRiskAgeHours,
    IReadOnlyList<TrendOutstandingRisk> OutstandingRiskRows,
    int RestoreTestsPerformed);

public record TrendComparisonResult(TrendPeriodResult PeriodA, TrendPeriodResult PeriodB);

/// <summary>
/// Compares two time periods for health coverage, outstanding-risk age, repeated failures, backup
/// duration/size, and restore-test activity (U22) — pure (no I/O, no DB), built from already-fetched
/// U10 snapshot rows and raw dated events so the comparison is fully unit-testable. Deliberately
/// never re-derives a past health status from today's BackupHealthCalculator/instance list; a
/// period with no snapshot evidence is flagged, not interpolated.
/// </summary>
public static class TrendsReportBuilder
{
    /// <summary>Instances with 2+ failed backup attempts within the period count as a repeated failure, not just one bad run.</summary>
    public const int RepeatedFailureThreshold = 2;

    public static TrendComparisonResult Compare(TrendPeriodInput periodA, TrendPeriodInput periodB) =>
        new(BuildPeriod(periodA), BuildPeriod(periodB));

    public static TrendPeriodResult BuildPeriod(TrendPeriodInput input)
    {
        var repeatedFailures = input.BackupEvents
            .Where(e => e.Status == BackupStatus.Failed)
            .GroupBy(e => e.InstanceName)
            .Select(g => new RepeatedFailureRow(g.Key, g.Count()))
            .Where(r => r.FailureCount >= RepeatedFailureThreshold)
            .OrderByDescending(r => r.FailureCount)
            .ThenBy(r => r.InstanceName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var durations = input.BackupEvents.Where(e => e.DurationMinutes is not null).Select(e => (double)e.DurationMinutes!.Value).ToList();
        var sizes = input.BackupEvents.Where(e => e.SizeMb is not null).Select(e => (double)e.SizeMb!.Value).ToList();

        var outstandingRisks = input.OutstandingRisks.OrderBy(r => r.OpenedAtUtc).ToList();
        var riskAgesHours = outstandingRisks.Select(r => (input.ToUtc - r.OpenedAtUtc).TotalHours).ToList();

        if (input.SnapshotRows is null)
        {
            return new TrendPeriodResult(
                input.FromUtc, input.ToUtc, MissingEvidence: true, SnapshotGeneratedAtUtc: null,
                InventoryCount: null, OnTrackCount: null, AtRiskCount: null, OnTrackPct: null, AtRiskRows: [],
                RestoreTestedCount: null, RestoreTestedPct: null,
                RepeatedFailureInstanceCount: repeatedFailures.Count, RepeatedFailureRows: repeatedFailures,
                BackupEventCount: input.BackupEvents.Count,
                AvgBackupDurationMinutes: durations.Count > 0 ? durations.Average() : null,
                AvgBackupSizeMb: sizes.Count > 0 ? sizes.Average() : null,
                AverageOutstandingRiskAgeHours: riskAgesHours.Count > 0 ? riskAgesHours.Average() : null,
                OutstandingRiskRows: outstandingRisks,
                RestoreTestsPerformed: input.RestoreTestTimestamps.Count);
        }

        var rows = input.SnapshotRows;
        var onTrack = rows.Count(r => r.Health == BackupHealth.OnTrack);
        var atRiskRows = rows.Where(r => r.Health != BackupHealth.OnTrack).OrderBy(r => r.InstanceName, StringComparer.OrdinalIgnoreCase).ToList();
        var restoreTested = rows.Count(r => r.RestoreTested);

        return new TrendPeriodResult(
            input.FromUtc, input.ToUtc, MissingEvidence: false, input.SnapshotGeneratedAtUtc,
            InventoryCount: rows.Count,
            OnTrackCount: onTrack,
            AtRiskCount: atRiskRows.Count,
            OnTrackPct: rows.Count > 0 ? onTrack * 100.0 / rows.Count : null,
            AtRiskRows: atRiskRows,
            RestoreTestedCount: restoreTested,
            RestoreTestedPct: rows.Count > 0 ? restoreTested * 100.0 / rows.Count : null,
            RepeatedFailureInstanceCount: repeatedFailures.Count, RepeatedFailureRows: repeatedFailures,
            BackupEventCount: input.BackupEvents.Count,
            AvgBackupDurationMinutes: durations.Count > 0 ? durations.Average() : null,
            AvgBackupSizeMb: sizes.Count > 0 ? sizes.Average() : null,
            AverageOutstandingRiskAgeHours: riskAgesHours.Count > 0 ? riskAgesHours.Average() : null,
            OutstandingRiskRows: outstandingRisks,
            RestoreTestsPerformed: input.RestoreTestTimestamps.Count);
    }
}
