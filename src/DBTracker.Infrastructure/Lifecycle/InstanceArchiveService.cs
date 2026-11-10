using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using DBTracker.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.Lifecycle;

public record ArchivedInstanceRow(int Id, string Name, string Application, string Company, DateTime? ArchivedAtUtc, string ArchivedBy, string ArchiveReason);

/// <summary>
/// Archive/unarchive with preview, attribution and audit (U14). Archiving never deletes anything —
/// backup/restore/change history stays attached to the instance row, which the background scan/
/// alert/report sweeps already exclude via their own `!IsArchived` filters; unarchiving makes the
/// instance visible to those sweeps again on their next run, which is exactly "health/alert
/// eligibility recomputed" since nothing about health is cached.
/// </summary>
public class InstanceArchiveService(IDbContextFactory<DBTrackerDbContext> factory, AuditRecorder audit)
{
    public async Task<ArchiveImpactCounts> PreviewAsync(int instanceId)
    {
        using var db = await factory.CreateDbContextAsync();
        var instance = await db.DatabaseInstances
            .AsNoTracking()
            .Include(i => i.BackupRecords)
            .FirstOrDefaultAsync(i => i.Id == instanceId);

        if (instance is null)
        {
            return new ArchiveImpactCounts(false, false, 0, false);
        }

        var health = BackupHealthCalculator.Compute(instance, instance.BackupRecords.ToList(), DateTime.UtcNow);
        var alertable = health is BackupHealth.Overdue or BackupHealth.Failed;
        var openIncidents = await db.Incidents.CountAsync(i => i.DatabaseInstanceId == instanceId && i.ClosedAtUtc == null);

        return new ArchiveImpactCounts(
            BackgroundScanConfigured: !string.IsNullOrWhiteSpace(instance.BackupFileLocation),
            AlertableNow: alertable,
            OpenIncidentCount: openIncidents,
            IncludedInScheduledReports: true);
    }

    public async Task<bool> ArchiveAsync(int instanceId, string reason, string actorId, string actorName)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A reason is required to archive an instance.", nameof(reason));
        }

        using var db = await factory.CreateDbContextAsync();
        var instance = await db.DatabaseInstances.FirstOrDefaultAsync(i => i.Id == instanceId);
        if (instance is null || instance.IsArchived)
        {
            return false;
        }

        var now = DateTime.UtcNow;
        instance.IsArchived = true;
        instance.ArchivedAtUtc = now;
        instance.ArchivedBy = actorName;
        instance.ArchiveReason = reason;
        instance.UpdatedAtUtc = now;
        await db.SaveChangesAsync();

        await audit.RecordUserActionAsync(actorId, actorName, "InstanceArchived", "DatabaseInstance", instanceId.ToString(), instance.Name, reason);
        return true;
    }

    public async Task<bool> UnarchiveAsync(int instanceId, string actorId, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        var instance = await db.DatabaseInstances.FirstOrDefaultAsync(i => i.Id == instanceId);
        if (instance is null || !instance.IsArchived)
        {
            return false;
        }

        var previousReason = instance.ArchiveReason;
        instance.IsArchived = false;
        instance.ArchivedAtUtc = null;
        instance.ArchivedBy = "";
        instance.ArchiveReason = "";
        instance.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // Nothing about health/alert eligibility is cached on the row — the next dashboard/list
        // read, scan sweep and alert sweep all recompute from scratch and will simply stop
        // excluding this instance now that IsArchived is false.
        await audit.RecordUserActionAsync(actorId, actorName, "InstanceUnarchived", "DatabaseInstance", instanceId.ToString(), instance.Name, $"Previously archived: {previousReason}");
        return true;
    }

    public async Task<List<ArchivedInstanceRow>> SearchArchivedAsync(string? search)
    {
        using var db = await factory.CreateDbContextAsync();
        var query = db.DatabaseInstances.AsNoTracking().Where(i => i.IsArchived);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(i => i.Name.Contains(s) || i.Application.Contains(s) || i.Company.Contains(s));
        }

        return await query
            .OrderByDescending(i => i.ArchivedAtUtc)
            .Select(i => new ArchivedInstanceRow(i.Id, i.Name, i.Application, i.Company, i.ArchivedAtUtc, i.ArchivedBy, i.ArchiveReason))
            .ToListAsync();
    }
}
