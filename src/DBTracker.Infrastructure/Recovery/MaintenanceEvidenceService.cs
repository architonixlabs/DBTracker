using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using DBTracker.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.Recovery;

public record MaintenanceEvidenceRow(MaintenanceEvidenceRecord Record, MaintenanceEvidenceStatus Status);

/// <summary>
/// D18 — integrity/HA/maintenance-job evidence, deliberately separate from backup-health plumbing:
/// nothing here ever reads or writes BackupRecord/BackupHealth, and nothing in BackupHealthCalculator
/// reads this table. A reviewed external monitoring/ticket link (Source != ManuallyEntered) requires
/// SourceReference — enforced here as well as in the UI, since this is also where an import path
/// would land.
/// </summary>
public class MaintenanceEvidenceService(IDbContextFactory<DBTrackerDbContext> factory, AuditRecorder audit)
{
    public async Task<List<MaintenanceEvidenceRow>> ListForInstanceAsync(int instanceId)
    {
        using var db = await factory.CreateDbContextAsync();
        var records = await db.MaintenanceEvidenceRecords.AsNoTracking()
            .Include(r => r.RelatedDependency)
            .Where(r => r.DatabaseInstanceId == instanceId)
            .OrderByDescending(r => r.RanAtUtc)
            .ToListAsync();

        var now = DateTime.UtcNow;
        return records.Select(r => new MaintenanceEvidenceRow(r, MaintenanceEvidenceStalenessClassifier.Classify(r, now))).ToList();
    }

    public async Task<(bool Ok, string Error)> RecordAsync(MaintenanceEvidenceRecord record, string actorName)
    {
        if (record.Source != MaintenanceEvidenceSource.ManuallyEntered && string.IsNullOrWhiteSpace(record.SourceReference))
        {
            return (false, "A reviewed external monitoring or ticket link requires a source reference.");
        }

        using var db = await factory.CreateDbContextAsync();
        record.RecordedAtUtc = DateTime.UtcNow;
        record.RecordedBy = actorName;
        db.MaintenanceEvidenceRecords.Add(record);
        await db.SaveChangesAsync();

        var instance = await db.DatabaseInstances.FirstAsync(i => i.Id == record.DatabaseInstanceId);
        await audit.RecordUserActionAsync("", actorName, "MaintenanceEvidenceRecorded", "DatabaseInstance", record.DatabaseInstanceId.ToString(), instance.Name,
            reason: $"{record.CheckType}: {record.Outcome} ({record.Source})");
        return (true, "");
    }
}
