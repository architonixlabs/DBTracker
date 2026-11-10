using DBTracker.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.Queries;

/// <summary>
/// Uses its own short-lived DbContext per call (via the factory) rather than the ambient scoped
/// one — pages that support manual/periodic refresh (U03) call this repeatedly while their own
/// scoped context may be mid-mutation (e.g. logging a backup), so a shared context would race the
/// same way NotificationService's comment elsewhere in this app describes.
/// </summary>
public class InstanceOverviewService(IDbContextFactory<DBTrackerDbContext> factory)
{
    public async Task<List<InstanceOverview>> GetAllAsync(bool includeArchived = false)
    {
        using var db = await factory.CreateDbContextAsync();

        var instances = await db.DatabaseInstances
            .AsNoTracking()
            .Where(i => includeArchived || !i.IsArchived)
            .Include(i => i.BackupRecords)
            .Include(i => i.RestoreEvents)
            .AsSplitQuery()
            .ToListAsync();

        var now = DateTime.UtcNow;

        return instances
            .Select(instance =>
            {
                var records = instance.BackupRecords.ToList();
                var last = records.OrderByDescending(r => r.TimestampUtc).FirstOrDefault();
                return new InstanceOverview
                {
                    Instance = instance,
                    Health = BackupHealthCalculator.Compute(instance, records, now),
                    LastBackupUtc = BackupHealthCalculator.LastSuccessfulBackupUtc(records),
                    LastRecord = last
                };
            })
            .ToList();
    }

    public async Task<InstanceOverview?> GetByIdAsync(int id)
    {
        using var db = await factory.CreateDbContextAsync();

        var instance = await db.DatabaseInstances
            .AsNoTracking()
            .Include(i => i.BackupRecords)
            .Include(i => i.RestoreEvents)
            .AsSplitQuery()
            .FirstOrDefaultAsync(i => i.Id == id);

        if (instance is null)
        {
            return null;
        }

        var records = instance.BackupRecords.ToList();
        var now = DateTime.UtcNow;
        var last = records.OrderByDescending(r => r.TimestampUtc).FirstOrDefault();

        return new InstanceOverview
        {
            Instance = instance,
            Health = BackupHealthCalculator.Compute(instance, records, now),
            LastBackupUtc = BackupHealthCalculator.LastSuccessfulBackupUtc(records),
            LastRecord = last
        };
    }
}
