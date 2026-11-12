using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using DBTracker.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace DBTracker.Infrastructure.Lifecycle;

public record ArchiveOutcome(bool Ok, string Message);

public record ArchivedInstanceRow(int Id, string Name, string Application, string Company, DateTime? ArchivedAtUtc, string ArchivedBy, string ArchiveReason);

/// <summary>Archive and unarchive with preview, attribution and audit. Nothing is deleted; sweeps skip archived instances and pick them up again after unarchive.</summary>
public class InstanceArchiveService(IDbContextFactory<DBTrackerDbContext> factory, AuditRecorder audit, IConfiguration configuration)
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

        // Snapshot rows are frozen JSON; match the serialized name, which is how the row stores it.
        var nameToken = $"\"InstanceName\":{System.Text.Json.JsonSerializer.Serialize(instance.Name)}";
        var snapshotCount = await db.ComplianceSnapshots.CountAsync(s => s.RowsJson.Contains(nameToken));

        return new ArchiveImpactCounts(
            BackgroundScanConfigured: !string.IsNullOrWhiteSpace(instance.BackupFileLocation),
            AlertableNow: alertable,
            OpenIncidentCount: openIncidents,
            // Reports and the Compliance view read every non-archived instance; there is no other report filter to consult.
            IncludedInScheduledReports: !instance.IsArchived,
            ScheduledReportIntervalDays: configuration.GetValue("Reports:IntervalDays", 7),
            DistributionListConfigured: !string.IsNullOrWhiteSpace(configuration["Reports:DistributionList"]),
            SavedSnapshotCount: snapshotCount);
    }

    /// <summary>Archives a record that is not being retired (a duplicate or one entered by mistake). Refused while a
    /// retirement checklist is open for the instance, so a real decommission can't skip the checklist's gates.</summary>
    public async Task<ArchiveOutcome> ArchiveAsync(int instanceId, string reason, string actorId, string actorName)
    {
        using (var db = await factory.CreateDbContextAsync())
        {
            var openChecklistId = await db.RetirementChecklists
                .Where(c => c.DatabaseInstanceId == instanceId && c.RetiredAtUtc == null && c.CancelledAtUtc == null)
                .Select(c => (int?)c.Id)
                .FirstOrDefaultAsync();
            if (openChecklistId is { } checklistId)
            {
                return new ArchiveOutcome(false,
                    $"Retirement checklist #{checklistId} is open for this instance. Finish the retirement there, or cancel the checklist, instead of archiving directly.");
            }
        }

        return await ArchiveCoreAsync(instanceId, reason, actorId, actorName, $"Not a retirement (duplicate or entered by mistake): {reason}");
    }

    /// <summary>Archives an instance whose retirement checklist has passed. Only RetirementChecklistService calls this.</summary>
    public Task<ArchiveOutcome> ArchiveForRetirementAsync(int instanceId, int checklistId, string reason, string actorId, string actorName) =>
        ArchiveCoreAsync(instanceId, reason, actorId, actorName, $"Retired through checklist #{checklistId}: {reason}");

    private async Task<ArchiveOutcome> ArchiveCoreAsync(int instanceId, string reason, string actorId, string actorName, string auditReason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A reason is required to archive an instance.", nameof(reason));
        }

        using var db = await factory.CreateDbContextAsync();
        var instance = await db.DatabaseInstances.FirstOrDefaultAsync(i => i.Id == instanceId);
        if (instance is null || instance.IsArchived)
        {
            return new ArchiveOutcome(false, "The instance could not be archived (it may already be archived).");
        }

        var now = DateTime.UtcNow;
        instance.IsArchived = true;
        instance.ArchivedAtUtc = now;
        instance.ArchivedBy = actorName;
        instance.ArchiveReason = reason;
        instance.UpdatedAtUtc = now;
        await db.SaveChangesAsync();

        await audit.RecordUserActionAsync(actorId, actorName, "InstanceArchived", "DatabaseInstance", instanceId.ToString(), instance.Name, auditReason);
        return new ArchiveOutcome(true, "");
    }

    /// <summary>Brings an archived instance back. A reason is required and recorded with the previous archive reason.</summary>
    public async Task<bool> UnarchiveAsync(int instanceId, string reason, string actorId, string actorName)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A reason is required to unarchive an instance.", nameof(reason));
        }
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

        // Health isn't cached, so the next read and sweep recompute it now that IsArchived is false.
        await audit.RecordUserActionAsync(actorId, actorName, "InstanceUnarchived", "DatabaseInstance", instanceId.ToString(), instance.Name,
            $"{reason.Trim()} (previously archived: {previousReason})",
            beforeJson: System.Text.Json.JsonSerializer.Serialize(new { IsArchived = true, ArchiveReason = previousReason }),
            afterJson: System.Text.Json.JsonSerializer.Serialize(new { IsArchived = false }));
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
