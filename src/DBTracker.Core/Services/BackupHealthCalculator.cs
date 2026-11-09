using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

public static class BackupHealthCalculator
{
    /// <summary>
    /// Computes backup health for an instance as of <paramref name="asOfUtc"/>.
    /// Failed beats overdue: a fresh failure is worse than an on-time success streak with an
    /// older failure buried in history.
    /// </summary>
    public static BackupHealth Compute(DatabaseInstance instance, IReadOnlyCollection<BackupRecord> records, DateTime asOfUtc)
    {
        if (records.Count == 0)
        {
            return BackupHealth.NeverBackedUp;
        }

        var mostRecent = records.OrderByDescending(r => r.TimestampUtc).First();

        if (mostRecent.Status == BackupStatus.Failed || mostRecent.Status == BackupStatus.Missed)
        {
            return BackupHealth.Failed;
        }

        var deadline = mostRecent.TimestampUtc.AddHours(instance.ExpectedBackupIntervalHours);
        return asOfUtc > deadline ? BackupHealth.Overdue : BackupHealth.OnTrack;
    }

    public static DateTime? LastSuccessfulBackupUtc(IReadOnlyCollection<BackupRecord> records)
    {
        return records
            .Where(r => r.Status == BackupStatus.Success)
            .OrderByDescending(r => r.TimestampUtc)
            .Select(r => (DateTime?)r.TimestampUtc)
            .FirstOrDefault();
    }
}
