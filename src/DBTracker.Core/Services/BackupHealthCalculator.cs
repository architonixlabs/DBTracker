using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>Plain-language explanation of a health result (U04) — the evidence behind it and the next deadline, kept separate from scan freshness and maintenance suppression so none of the three get conflated.</summary>
public record HealthExplanation(
    BackupHealth Health,
    DateTime? LastSuccessUtc,
    DateTime? LastAttemptUtc,
    BackupStatus? LastAttemptStatus,
    int GoverningIntervalHours,
    DateTime? NextDueUtc,
    TimeSpan? OverdueBy,
    bool IsSuppressedByMaintenance);

public static class BackupHealthCalculator
{
    /// <summary>
    /// Computes backup health for an instance as of <paramref name="asOfUtc"/>.
    /// Failed beats overdue: a fresh failure is worse than an on-time success streak with an
    /// older failure buried in history. Records timestamped after <paramref name="asOfUtc"/> are
    /// ignored — evidence from the future can't yet be used to judge an earlier instant (U04).
    /// Ties on timestamp break by Id (insertion order), so results are deterministic.
    /// </summary>
    public static BackupHealth Compute(DatabaseInstance instance, IReadOnlyCollection<BackupRecord> records, DateTime asOfUtc)
    {
        var mostRecent = MostRecentAsOf(records, asOfUtc);
        if (mostRecent is null)
        {
            return BackupHealth.NeverBackedUp;
        }

        if (mostRecent.Status == BackupStatus.Failed || mostRecent.Status == BackupStatus.Missed)
        {
            return BackupHealth.Failed;
        }

        var deadline = mostRecent.TimestampUtc.AddHours(instance.ExpectedBackupIntervalHours);
        return asOfUtc > deadline ? BackupHealth.Overdue : BackupHealth.OnTrack;
    }

    public static DateTime? LastSuccessfulBackupUtc(IReadOnlyCollection<BackupRecord> records) =>
        LastSuccessfulBackupUtc(records, DateTime.MaxValue);

    public static DateTime? LastSuccessfulBackupUtc(IReadOnlyCollection<BackupRecord> records, DateTime asOfUtc)
    {
        return records
            .Where(r => r.Status == BackupStatus.Success && r.TimestampUtc <= asOfUtc)
            .OrderByDescending(r => r.TimestampUtc)
            .ThenByDescending(r => r.Id)
            .Select(r => (DateTime?)r.TimestampUtc)
            .FirstOrDefault();
    }

    /// <summary>Builds the plain-language reasoning behind a health result (U04) — last success, latest attempt, the governing interval, next due time and how overdue it is, with maintenance suppression called out separately rather than folded into the health value itself.</summary>
    public static HealthExplanation Explain(DatabaseInstance instance, IReadOnlyCollection<BackupRecord> records, DateTime asOfUtc)
    {
        var health = Compute(instance, records, asOfUtc);
        var lastSuccessUtc = LastSuccessfulBackupUtc(records, asOfUtc);
        var mostRecent = MostRecentAsOf(records, asOfUtc);

        DateTime? nextDueUtc = lastSuccessUtc?.AddHours(instance.ExpectedBackupIntervalHours);
        TimeSpan? overdueBy = nextDueUtc is { } due && asOfUtc > due ? asOfUtc - due : null;

        return new HealthExplanation(
            Health: health,
            LastSuccessUtc: lastSuccessUtc,
            LastAttemptUtc: mostRecent?.TimestampUtc,
            LastAttemptStatus: mostRecent?.Status,
            GoverningIntervalHours: instance.ExpectedBackupIntervalHours,
            NextDueUtc: nextDueUtc,
            OverdueBy: overdueBy,
            IsSuppressedByMaintenance: AlertEvaluator.IsSuppressedByMaintenance(instance.MaintenanceUntilUtc, asOfUtc));
    }

    private static BackupRecord? MostRecentAsOf(IReadOnlyCollection<BackupRecord> records, DateTime asOfUtc) =>
        records
            .Where(r => r.TimestampUtc <= asOfUtc)
            .OrderByDescending(r => r.TimestampUtc)
            .ThenByDescending(r => r.Id)
            .FirstOrDefault();
}
