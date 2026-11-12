using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using DBTracker.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.Import;

public record HistoryImportOutcome(bool Ok, int BackupsCreated, int RestoresCreated, int SkippedAtApply, string Message);

/// <summary>Previews and applies a backup and restore history CSV. Applying writes every new row in one transaction (all or nothing),
/// re-checks the repeat-safety key against the database so a second tab or a double click can't add duplicates, and records one
/// audit event for the batch. Entries are Source = Imported with the importing user.</summary>
public class EvidenceHistoryImportService(IDbContextFactory<DBTrackerDbContext> factory, AuditRecorder audit)
{
    public async Task<HistoryImportPreview> PreviewAsync(string csvContent, string fileName, string importedBy)
    {
        using var db = await factory.CreateDbContextAsync();
        var instances = await db.DatabaseInstances.AsNoTracking()
            .Select(i => new { i.Id, i.Name, i.IsArchived })
            .ToListAsync();

        var byName = instances.GroupBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToList();
        var idsByName = byName
            .Where(g => g.Count() == 1 && !g.First().IsArchived)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
        var blocked = byName
            .Where(g => g.Count() > 1 || g.First().IsArchived)
            .ToDictionary(
                g => g.Key,
                g => g.Count() > 1
                    ? $"Name '{g.Key}' matches {g.Count()} instances (including archived ones), so the history can't be placed. Rename one first."
                    : $"'{g.Key}' is archived. Unarchive it first to load its history.",
                StringComparer.OrdinalIgnoreCase);

        var existing = await LoadExistingAsync(db, idsByName.Values.ToList());
        return EvidenceHistoryCsvImporter.ParseForImport(csvContent, idsByName, blocked, existing, DateTime.UtcNow, importedBy, fileName);
    }

    public async Task<HistoryImportOutcome> ApplyAsync(HistoryImportPreview preview, string fileName, string userId, string userName)
    {
        var creates = preview.Rows.Where(r => r.Plan == ImportRowPlan.Create).ToList();
        if (creates.Count == 0)
        {
            return new HistoryImportOutcome(false, 0, 0, 0, "Nothing to import.");
        }

        using var db = await factory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();

        var instanceIds = creates.Select(r => r.InstanceId).Distinct().ToList();
        var active = await db.DatabaseInstances.Where(i => instanceIds.Contains(i.Id) && !i.IsArchived).Select(i => i.Id).ToListAsync();
        if (active.Count != instanceIds.Count)
        {
            return new HistoryImportOutcome(false, 0, 0, 0, "An instance in this file was archived or removed after the preview, so nothing was saved. Upload the file again.");
        }

        var stored = (await LoadExistingAsync(db, instanceIds))
            .Select(e => EvidenceHistoryCsvImporter.MatchKey(e.InstanceId, e.Kind, e.OccurredUtc, e.TypeOrReason))
            .ToHashSet(StringComparer.Ordinal);

        int backups = 0, restores = 0, skipped = 0;
        foreach (var row in creates)
        {
            if (!stored.Add(EvidenceHistoryCsvImporter.MatchKey(row.InstanceId, row.Kind, row.OccurredUtc, row.TypeOrReason)))
            {
                skipped++;
                continue;
            }

            if (row.Backup is { } b)
            {
                b.RecordedByUserName = userName;
                db.BackupRecords.Add(b);
                backups++;
            }
            else if (row.Restore is { } r)
            {
                r.RecordedByUserName = userName;
                db.RestoreEvents.Add(r);
                restores++;
            }
        }

        try
        {
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch (DbUpdateException)
        {
            return new HistoryImportOutcome(false, 0, 0, 0, "The import failed and nothing was saved. Upload the file again to rebuild the preview.");
        }

        await audit.RecordUserActionAsync(userId, userName, "EvidenceHistoryImported", "CsvImport", "", fileName,
            reason: $"{backups} backup and {restores} restore entries imported as Source = Imported; {preview.SkipCount + skipped} already recorded, " +
                    $"{preview.AmbiguousCount} ambiguous, {preview.Errors.Count} error row(s). Imported entries never count as verified evidence.",
            afterJson: System.Text.Json.JsonSerializer.Serialize(new
            {
                BackupsCreated = backups,
                RestoresCreated = restores,
                Instances = creates.Select(c => c.InstanceName).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n).ToList(),
                EarliestUtc = creates.Min(c => c.OccurredUtc),
                LatestUtc = creates.Max(c => c.OccurredUtc)
            }));

        return new HistoryImportOutcome(true, backups, restores, skipped, "");
    }

    private static async Task<List<HistoryExistingEvent>> LoadExistingAsync(DBTrackerDbContext db, List<int> instanceIds)
    {
        var backups = await db.BackupRecords.AsNoTracking()
            .Where(b => instanceIds.Contains(b.DatabaseInstanceId))
            .Select(b => new { b.DatabaseInstanceId, b.TimestampUtc, b.Type, b.Status })
            .ToListAsync();
        var restores = await db.RestoreEvents.AsNoTracking()
            .Where(r => instanceIds.Contains(r.DatabaseInstanceId))
            .Select(r => new { r.DatabaseInstanceId, r.TimestampUtc, r.Reason, r.Outcome })
            .ToListAsync();

        return backups.Select(b => new HistoryExistingEvent(b.DatabaseInstanceId, HistoryEventKind.Backup, b.TimestampUtc, b.Type.ToString(), b.Status.ToString()))
            .Concat(restores.Select(r => new HistoryExistingEvent(r.DatabaseInstanceId, HistoryEventKind.Restore, r.TimestampUtc, r.Reason.ToString(), r.Outcome.ToString())))
            .ToList();
    }
}
