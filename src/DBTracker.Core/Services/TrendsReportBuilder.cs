using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>Raw dated backup events for a trend period, not a recomputed health status.</summary>
public record TrendBackupEvent(string InstanceName, DateTime TimestampUtc, BackupStatus Status, double? SizeMb, double? DurationMinutes);

/// <summary>An incident open at the period's end, from its stored timestamps.</summary>
public record TrendOutstandingRisk(string InstanceName, DateTime OpenedAtUtc, BackupHealth TriggerHealth);

public record RepeatedFailureRow(string InstanceName, int FailureCount);

/// <summary>Inputs for one side of a trend comparison. Snapshot rows come from the nearest snapshot at or before ToUtc; null is reported as missing evidence, never filled from today's live inventory.</summary>
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

/// <summary>Compares two periods for health coverage, risk age, repeated failures, backup duration/size and restore tests. Past health is never re-derived from today's data; a period without snapshot evidence is flagged.</summary>
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
