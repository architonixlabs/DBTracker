using DBTracker.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.Queries;

public class InstanceOverviewService(DBTrackerDbContext db)
{
    public async Task<List<InstanceOverview>> GetAllAsync(bool includeArchived = false)
    {
        var instances = await db.DatabaseInstances
            .AsNoTracking()
            .Where(i => includeArchived || !i.IsArchived)
            .Include(i => i.BackupRecords)
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
        var instance = await db.DatabaseInstances
            .AsNoTracking()
            .Include(i => i.BackupRecords)
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
