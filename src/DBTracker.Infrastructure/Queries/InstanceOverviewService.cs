using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.Queries;

/// <summary>Uses a short-lived DbContext per call: refreshing pages call this while their own scoped context may be mid-save.</summary>
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

    /// <summary>Lightweight dashboard/list projection: one subquery each for the latest record and latest success, instead of loading every instance's history.</summary>
    public async Task<List<InstanceListRow>> GetAllSummariesAsync(bool includeArchived = false)
    {
        using var db = await factory.CreateDbContextAsync();
        var now = DateTime.UtcNow;

        var projected = await db.DatabaseInstances
            .AsNoTracking()
            .Where(i => includeArchived || !i.IsArchived)
            .Select(i => new
            {
                Instance = i,
                OwnerTeamName = i.OwnerTeam != null ? i.OwnerTeam.Name : "",
                LatestRecord = i.BackupRecords
                    .Where(r => r.TimestampUtc <= now)
                    .OrderByDescending(r => r.TimestampUtc).ThenByDescending(r => r.Id)
                    .FirstOrDefault(),
                LatestSuccessUtc = i.BackupRecords
                    .Where(r => r.Status == BackupStatus.Success && r.TimestampUtc <= now)
                    .OrderByDescending(r => r.TimestampUtc).ThenByDescending(r => r.Id)
                    .Select(r => (DateTime?)r.TimestampUtc)
                    .FirstOrDefault()
            })
            .ToListAsync();

        return projected.Select(p => ToRow(p.Instance, p.LatestRecord, p.LatestSuccessUtc, now, p.OwnerTeamName)).ToList();
    }

    /// <summary>Filtered, sorted, paged Instances list from the same projection as <see cref="GetAllSummariesAsync"/>.</summary>
    public async Task<InstanceListResult> GetPagedAsync(InstanceListQuery query, bool includeArchived = false)
    {
        var rows = await GetAllSummariesAsync(includeArchived);
        return InstanceListFilter.Apply(rows, query);
    }

    private static InstanceListRow ToRow(DatabaseInstance instance, BackupRecord? latestRecord, DateTime? latestSuccessUtc, DateTime now, string ownerTeamName = "") =>
        new(
            instance.Id, instance.Name, instance.Engine, instance.Version, instance.Application,
            instance.Company, instance.Department, instance.Tags, instance.ContactName, instance.ContactEmail,
            instance.Role, instance.Environment, instance.DatabaseIp,
            BackupHealthCalculator.ComputeFromLatest(instance, latestRecord, now),
            latestSuccessUtc,
            instance.MaintenanceUntilUtc,
            instance.BackupFileLocation, instance.LastScanUtc, instance.LastScanStatus, instance.LastScanError,
            instance.OwnerTeamId, ownerTeamName, instance.BackupContactEmail, instance.Region, instance.Cluster, instance.ReplicaSet,
            OwnershipStatusClassifier.Classify(instance.OwnerTeamId, instance.ContactEmail, instance.BackupContactEmail, instance.OwnershipConfirmedAtUtc));

    public async Task<InstanceOverview?> GetByIdAsync(int id)
    {
        using var db = await factory.CreateDbContextAsync();

        var instance = await db.DatabaseInstances
            .AsNoTracking()
            .Include(i => i.BackupRecords)
            .Include(i => i.RestoreEvents)
            .Include(i => i.OwnerTeam)
            .Include(i => i.AppliedPolicy)
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
