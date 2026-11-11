using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>The evidence behind a health result and the next deadline, kept separate from scan freshness and maintenance suppression.</summary>
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
    /// <summary>Computes backup health as of <paramref name="asOfUtc"/>. A fresh failure beats an on-time streak; records after <paramref name="asOfUtc"/> are ignored, and timestamp ties break by Id.</summary>
    public static BackupHealth Compute(DatabaseInstance instance, IReadOnlyCollection<BackupRecord> records, DateTime asOfUtc) =>
        ComputeFromLatest(instance, MostRecentAsOf(records, asOfUtc), asOfUtc);

    /// <summary>Same rule as <see cref="Compute"/> from just the latest record, so list and dashboard queries don't load every BackupRecord.</summary>
    public static BackupHealth ComputeFromLatest(DatabaseInstance instance, BackupRecord? mostRecentAsOf, DateTime asOfUtc)
    {
        if (mostRecentAsOf is null)
        {
            return BackupHealth.NeverBackedUp;
        }

        if (mostRecentAsOf.Status == BackupStatus.Failed || mostRecentAsOf.Status == BackupStatus.Missed)
        {
            return BackupHealth.Failed;
        }

        var deadline = mostRecentAsOf.TimestampUtc.AddHours(instance.ExpectedBackupIntervalHours);
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

    /// <summary>Builds the reasoning behind a health result: last success, latest attempt, interval, next due, overdue duration, and maintenance suppression called out separately.</summary>
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
