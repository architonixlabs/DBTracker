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

    /// <summary>Filtered, sorted, paged Instances list (U06). Filtering, the health facet, sorting, counting and paging all run in SQL,
    /// so only one page of rows is materialized. Same rules as <see cref="InstanceListFilter.Apply"/>, which stays the in-memory reference.</summary>
    /// <remarks>Text matches use the database collation (case-insensitive by default on SQL Server). Tag matching ignores spaces inside
    /// the stored tag list, so "two words" also matches a stored "twowords".</remarks>
    public async Task<InstanceListResult> GetPagedAsync(InstanceListQuery query, bool includeArchived = false)
    {
        using var db = await factory.CreateDbContextAsync();
        var now = DateTime.UtcNow;

        var instances = db.DatabaseInstances.AsNoTracking().Where(i => includeArchived || !i.IsArchived);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            instances = instances.Where(i =>
                i.Name.Contains(term) || i.Application.Contains(term) || i.Company.Contains(term) ||
                i.Department.Contains(term) || i.ContactName.Contains(term) || i.ContactEmail.Contains(term) ||
                i.DatabaseIp.Contains(term));
        }
        if (query.Engine is { } engine) instances = instances.Where(i => i.Engine == engine);
        if (query.Role is { } role) instances = instances.Where(i => i.Role == role);
        if (query.Environment is { } env) instances = instances.Where(i => i.Environment == env);
        if (!string.IsNullOrWhiteSpace(query.Company)) instances = instances.Where(i => i.Company == query.Company);
        if (!string.IsNullOrWhiteSpace(query.Department)) instances = instances.Where(i => i.Department == query.Department);
        if (!string.IsNullOrWhiteSpace(query.Owner)) instances = instances.Where(i => i.ContactName == query.Owner);
        if (!string.IsNullOrWhiteSpace(query.Region)) instances = instances.Where(i => i.Region == query.Region);
        if (!string.IsNullOrWhiteSpace(query.Cluster)) instances = instances.Where(i => i.Cluster == query.Cluster);
        if (!string.IsNullOrWhiteSpace(query.Tag))
        {
            var needle = "," + query.Tag.Trim().Replace(" ", "") + ",";
            instances = instances.Where(i => ("," + i.Tags.Replace(";", ",").Replace(" ", "") + ",").Contains(needle));
        }
        if (query.MyInstancesOnly)
        {
            var email = query.MyEmail ?? "";
            var teamIds = (query.MyTeamIds ?? new HashSet<int>()).ToList();
            instances = instances.Where(i =>
                (email != "" && (i.ContactEmail == email || i.BackupContactEmail == email)) ||
                (i.OwnerTeamId != null && teamIds.Contains(i.OwnerTeamId.Value)));
        }

        // Health as a risk rank in SQL, the same rule as BackupHealthCalculator.ComputeFromLatest:
        // 0 Failed (latest is Failed/Missed), 1 NeverBackedUp, 2 Overdue (past latest + interval), 3 OnTrack.
        var ranked = instances
            .Select(i => new
            {
                Instance = i,
                Latest = i.BackupRecords
                    .Where(r => r.TimestampUtc <= now)
                    .OrderByDescending(r => r.TimestampUtc).ThenByDescending(r => r.Id)
                    .Select(r => new { r.TimestampUtc, r.Status })
                    .FirstOrDefault()
            })
            .Select(x => new
            {
                x.Instance,
                Rank = x.Latest == null ? 1
                    : x.Latest.Status == BackupStatus.Failed || x.Latest.Status == BackupStatus.Missed ? 0
                    : x.Latest.TimestampUtc.AddHours(x.Instance.ExpectedBackupIntervalHours) < now ? 2
                    : 3
            });

        if (query.Health is { } health)
        {
            var rank = RiskRank(health);
            ranked = ranked.Where(x => x.Rank == rank);
        }

        var totalCount = await ranked.CountAsync();
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 25 : query.PageSize, 1, InstanceListFilter.MaxPageSize);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        var page = Math.Clamp(query.Page <= 0 ? 1 : query.Page, 1, totalPages);

        var ordered = (query.Sort, query.Descending) switch
        {
            (InstanceSortKey.Risk, false) => ranked.OrderBy(x => x.Rank),
            (InstanceSortKey.Risk, true) => ranked.OrderByDescending(x => x.Rank),
            (InstanceSortKey.Engine, false) => ranked.OrderBy(x => x.Instance.Engine),
            (InstanceSortKey.Engine, true) => ranked.OrderByDescending(x => x.Instance.Engine),
            (InstanceSortKey.Company, false) => ranked.OrderBy(x => x.Instance.Company),
            (InstanceSortKey.Company, true) => ranked.OrderByDescending(x => x.Instance.Company),
            (InstanceSortKey.Environment, false) => ranked.OrderBy(x => x.Instance.Environment),
            (InstanceSortKey.Environment, true) => ranked.OrderByDescending(x => x.Instance.Environment),
            (_, false) => ranked.OrderBy(x => x.Instance.Name),
            (_, true) => ranked.OrderByDescending(x => x.Instance.Name),
        };

        // Name then Id as tie-breakers keep paging stable when many rows share the primary key.
        var pageIds = await ordered
            .ThenBy(x => x.Instance.Name).ThenBy(x => x.Instance.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => x.Instance.Id)
            .ToListAsync();

        var rows = await SummariesForAsync(db, pageIds, now);
        var byId = rows.ToDictionary(r => r.Id);
        return new InstanceListResult(pageIds.Where(byId.ContainsKey).Select(id => byId[id]).ToList(), totalCount, page, pageSize);
    }

    private static async Task<List<InstanceListRow>> SummariesForAsync(DBTrackerDbContext db, List<int> ids, DateTime now)
    {
        var projected = await db.DatabaseInstances
            .AsNoTracking()
            .Where(i => ids.Contains(i.Id))
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

    private static int RiskRank(BackupHealth health) => health switch
    {
        BackupHealth.Failed => 0,
        BackupHealth.NeverBackedUp => 1,
        BackupHealth.Overdue => 2,
        _ => 3
    };

    /// <summary>Overview rows for reports that need each instance's current state but not its backup history (U06).</summary>
    /// <remarks><see cref="DatabaseInstance.BackupRecords"/> holds only the latest record and the latest success (at most two rows), which is
    /// all health, last-success time and its evidence level need. <see cref="DatabaseInstance.RestoreEvents"/> is complete, because
    /// restore-test coverage needs every test and restores are far fewer than backups. Don't use these rows to list backup history.</remarks>
    public async Task<List<InstanceOverview>> GetAllLatestAsync(bool includeArchived = false)
    {
        using var db = await factory.CreateDbContextAsync();
        var now = DateTime.UtcNow;

        var projected = await db.DatabaseInstances
            .AsNoTracking()
            .Where(i => includeArchived || !i.IsArchived)
            .Select(i => new
            {
                Instance = i,
                Latest = i.BackupRecords
                    .Where(r => r.TimestampUtc <= now)
                    .OrderByDescending(r => r.TimestampUtc).ThenByDescending(r => r.Id)
                    .FirstOrDefault(),
                LatestSuccess = i.BackupRecords
                    .Where(r => r.Status == BackupStatus.Success && r.TimestampUtc <= now)
                    .OrderByDescending(r => r.TimestampUtc).ThenByDescending(r => r.Id)
                    .FirstOrDefault()
            })
            .ToListAsync();

        var restores = await db.RestoreEvents
            .AsNoTracking()
            .Where(r => includeArchived || !r.DatabaseInstance!.IsArchived)
            .ToListAsync();
        var restoresByInstance = restores.ToLookup(r => r.DatabaseInstanceId);

        return projected.Select(p =>
        {
            var instance = p.Instance;
            instance.BackupRecords = new[] { p.Latest, p.LatestSuccess }
                .Where(r => r is not null)
                .DistinctBy(r => r!.Id)
                .Select(r => r!)
                .ToList();
            instance.RestoreEvents = restoresByInstance[instance.Id].ToList();

            return new InstanceOverview
            {
                Instance = instance,
                Health = BackupHealthCalculator.ComputeFromLatest(instance, p.Latest, now),
                LastBackupUtc = p.LatestSuccess?.TimestampUtc,
                LastRecord = p.Latest
            };
        }).ToList();
    }

    /// <summary>Backup runs in a window for the Backup activity report, queried by date in SQL instead of loading every history (U06).
    /// Evidence level uses each instance's latest successful restore, the same rule as <see cref="BackupEvidenceClassifier.Classify(BackupRecord, IReadOnlyCollection{RestoreEvent})"/>.</summary>
    public async Task<List<BackupActivityRow>> GetBackupActivityAsync(DateTime fromUtc, DateTime toUtc, bool includeArchived = false)
    {
        using var db = await factory.CreateDbContextAsync();

        var records = await db.BackupRecords
            .AsNoTracking()
            .Where(r => r.TimestampUtc >= fromUtc && r.TimestampUtc <= toUtc)
            .Where(r => includeArchived || !r.DatabaseInstance!.IsArchived)
            .Select(r => new
            {
                r.DatabaseInstanceId,
                r.DatabaseInstance!.Name,
                r.DatabaseInstance.Company,
                r.DatabaseInstance.Department,
                r.DatabaseInstance.Engine,
                r.TimestampUtc,
                r.Type,
                r.Status,
                r.SizeMb,
                r.DurationMinutes,
                r.Source
            })
            .ToListAsync();

        var instanceIds = records.Select(r => r.DatabaseInstanceId).Distinct().ToList();
        var lastRestoreSuccess = await db.RestoreEvents
            .AsNoTracking()
            .Where(r => r.Outcome == RestoreOutcome.Success && r.Source != BackupSource.Imported && instanceIds.Contains(r.DatabaseInstanceId))
            .GroupBy(r => r.DatabaseInstanceId)
            .Select(g => new { InstanceId = g.Key, LastUtc = g.Max(r => r.TimestampUtc) })
            .ToDictionaryAsync(x => x.InstanceId, x => (DateTime?)x.LastUtc);

        return records
            .Select(r => new BackupActivityRow(
                r.Name, r.Company, r.Department, r.Engine, r.TimestampUtc, r.Type, r.Status, r.SizeMb, r.DurationMinutes, r.Source,
                BackupEvidenceClassifier.Classify(r.Status, r.Source, r.TimestampUtc, lastRestoreSuccess.GetValueOrDefault(r.DatabaseInstanceId))))
            .OrderByDescending(r => r.TimestampUtc)
            .ToList();
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
            OwnershipStatusClassifier.Classify(instance.OwnerTeamId, instance.ContactEmail, instance.BackupContactEmail, instance.OwnershipConfirmedAtUtc),
            instance.LastScanSuccessUtc);

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
