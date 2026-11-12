using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>Raw dated backup events for a trend period, not a recomputed health status.</summary>
public record TrendBackupEvent(string InstanceName, DateTime TimestampUtc, BackupStatus Status, double? SizeMb, double? DurationMinutes);

/// <summary>An incident open at the period's end, from its stored timestamps.</summary>
public record TrendOutstandingRisk(string InstanceName, DateTime OpenedAtUtc, BackupHealth TriggerHealth);

public record RepeatedFailureRow(string InstanceName, int FailureCount);

/// <summary>A backup policy edit recorded in the audit trail, shown against the period it happened in.</summary>
public record TrendPolicyChange(DateTime ChangedAtUtc, string PolicyName, string ChangedBy, string Summary);

/// <summary>A saved snapshot that could serve as a period's evidence.</summary>
public record TrendSnapshotCandidate(int Id, DateTime EffectiveAsOfUtc);

/// <summary>The snapshot chosen for a period, or why none could be used.</summary>
public record TrendSnapshotChoice(int? SnapshotId, string? MissingReason);

/// <summary>Inputs for one side of a trend comparison. Snapshot rows come from the nearest snapshot at or before ToUtc; null is reported as missing evidence, never filled from today's live inventory.</summary>
public record TrendPeriodInput(
    DateTime FromUtc,
    DateTime ToUtc,
    IReadOnlyList<ComplianceSnapshotRow>? SnapshotRows,
    DateTime? SnapshotGeneratedAtUtc,
    IReadOnlyList<TrendBackupEvent> BackupEvents,
    IReadOnlyList<DateTime> RestoreTestTimestamps,
    IReadOnlyList<TrendOutstandingRisk> OutstandingRisks,
    IReadOnlyList<TrendPolicyChange>? PolicyChanges = null,
    string? MissingEvidenceReason = null);

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
    int RestoreTestsPerformed)
{
    /// <summary>Restore-test recency frozen in the snapshot. Null when there is no snapshot or it predates recency being recorded.</summary>
    public int? RestoreCurrentCount { get; init; }
    public int? RestoreOverdueCount { get; init; }
    public int? RestoreNeverTestedCount { get; init; }
    public IReadOnlyList<ComplianceSnapshotRow> RestoreOverdueRows { get; init; } = [];
    public IReadOnlyList<TrendPolicyChange> PolicyChanges { get; init; } = [];
    public string? MissingEvidenceReason { get; init; }
}

public record TrendComparisonResult(TrendPeriodResult PeriodA, TrendPeriodResult PeriodB);

/// <summary>Compares two periods for health coverage, risk age, repeated failures, backup duration/size and restore tests. Past health is never re-derived from today's data; a period without snapshot evidence is flagged.</summary>
public static class TrendsReportBuilder
{
    /// <summary>Instances with 2+ failed backup attempts within the period count as a repeated failure, not just one bad run.</summary>
    public const int RepeatedFailureThreshold = 2;

    public static TrendComparisonResult Compare(TrendPeriodInput periodA, TrendPeriodInput periodB) =>
        new(BuildPeriod(periodA), BuildPeriod(periodB));

    /// <summary>Picks the latest snapshot taken within the period (start and end inclusive). A snapshot from before the period
    /// started describes an earlier time, so it is not used; the reason says so instead.</summary>
    public static TrendSnapshotChoice SelectSnapshot(IEnumerable<TrendSnapshotCandidate> candidates, DateTime fromUtc, DateTime toUtc)
    {
        var list = candidates.ToList();
        var inPeriod = list
            .Where(c => c.EffectiveAsOfUtc >= fromUtc && c.EffectiveAsOfUtc <= toUtc)
            .OrderByDescending(c => c.EffectiveAsOfUtc).ThenByDescending(c => c.Id)
            .FirstOrDefault();
        if (inPeriod is not null)
        {
            return new TrendSnapshotChoice(inPeriod.Id, null);
        }

        var earlier = list.Where(c => c.EffectiveAsOfUtc < fromUtc).OrderByDescending(c => c.EffectiveAsOfUtc).FirstOrDefault();
        return new TrendSnapshotChoice(null, earlier is null
            ? "No Compliance snapshot was saved during this period."
            : $"The latest snapshot before this period ends was taken {earlier.EffectiveAsOfUtc:yyyy-MM-dd HH:mm} UTC, before the period started, so it isn't used.");
    }

    public static TrendPeriodResult BuildPeriod(TrendPeriodInput input)
    {
        var result = BuildCore(input);
        var policyChanges = (input.PolicyChanges ?? [])
            .Where(c => c.ChangedAtUtc >= input.FromUtc && c.ChangedAtUtc <= input.ToUtc)
            .OrderBy(c => c.ChangedAtUtc)
            .ToList();

        if (input.SnapshotRows is not { } rows)
        {
            return result with
            {
                PolicyChanges = policyChanges,
                MissingEvidenceReason = input.MissingEvidenceReason ?? "No Compliance snapshot was saved during this period."
            };
        }

        // Older snapshots didn't record recency; their counts stay null instead of being worked out from today's data.
        var recorded = rows.All(r => r.RestoreRecency is not null);
        return result with
        {
            PolicyChanges = policyChanges,
            RestoreCurrentCount = recorded ? rows.Count(r => r.RestoreRecency == RestoreTestRecency.Current) : null,
            RestoreOverdueCount = recorded ? rows.Count(r => r.RestoreRecency == RestoreTestRecency.Overdue) : null,
            RestoreNeverTestedCount = recorded ? rows.Count(r => r.RestoreRecency == RestoreTestRecency.NeverTested) : null,
            RestoreOverdueRows = recorded
                ? rows.Where(r => r.RestoreRecency == RestoreTestRecency.Overdue).OrderBy(r => r.InstanceName, StringComparer.OrdinalIgnoreCase).ToList()
                : []
        };
    }

    private static TrendPeriodResult BuildCore(TrendPeriodInput input)
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
