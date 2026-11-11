using System.Text.Json;
using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using DBTracker.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.Lifecycle;

/// <summary>The curated release catalogue. Every change writes a revision snapshot and an audit event; a failed refresh only sets the staleness fields.</summary>
public class ReleaseCatalogService(IDbContextFactory<DBTrackerDbContext> factory, AuditRecorder audit)
{
    public async Task<List<ReleaseCatalogEntry>> ListAsync(DatabaseEngine? engine = null)
    {
        using var db = await factory.CreateDbContextAsync();
        var query = db.ReleaseCatalogEntries.AsNoTracking().AsQueryable();
        if (engine is { } e)
        {
            query = query.Where(c => c.Engine == e);
        }
        return await query.OrderBy(c => c.Engine).ThenBy(c => c.Family).ThenByDescending(c => c.Major).ToListAsync();
    }

    public async Task<ReleaseCatalogEntry?> GetAsync(int id)
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.ReleaseCatalogEntries.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
    }

    /// <summary>Stored catalogue entries for a family. Never calls a live vendor site.</summary>
    public async Task<List<ReleaseCatalogEntry>> FindApplicableAsync(DatabaseEngine engine, string family)
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.ReleaseCatalogEntries.AsNoTracking()
            .Where(c => c.Engine == engine && c.Family == family)
            .ToListAsync();
    }

    public async Task<ReleaseCatalogEntry> CreateAsync(ReleaseCatalogEntry entry, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        entry.CreatedAtUtc = DateTime.UtcNow;
        entry.UpdatedAtUtc = DateTime.UtcNow;
        entry.UpdatedBy = actorName;
        entry.RevisionNumber = 1;
        db.ReleaseCatalogEntries.Add(entry);
        await db.SaveChangesAsync();

        db.ReleaseCatalogRevisions.Add(new ReleaseCatalogRevision
        {
            ReleaseCatalogEntryId = entry.Id,
            RevisedBy = actorName,
            ChangeSummary = "Created",
            SnapshotJson = "{}"
        });
        await db.SaveChangesAsync();

        await audit.RecordUserActionAsync("", actorName, "ReleaseCatalogEntryCreated", "ReleaseCatalogEntry", entry.Id.ToString(), entry.ReleaseLabel);
        return entry;
    }

    /// <summary>Records a manual override. Reason, reviewer and optional expiry are required so an override can't quietly become permanent.</summary>
    public async Task<bool> ApplyManualOverrideAsync(int entryId, string reason, string reviewedBy, DateTime? expiresUtc, Action<ReleaseCatalogEntry> applyChanges, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        var entry = await db.ReleaseCatalogEntries.FirstOrDefaultAsync(c => c.Id == entryId);
        if (entry is null || string.IsNullOrWhiteSpace(reason) || string.IsNullOrWhiteSpace(reviewedBy))
        {
            return false;
        }

        var before = Snapshot(entry);
        applyChanges(entry);

        entry.IsManualOverride = true;
        entry.OverrideReason = reason;
        entry.OverrideReviewedBy = reviewedBy;
        entry.OverrideExpiresUtc = expiresUtc;
        entry.UpdatedAtUtc = DateTime.UtcNow;
        entry.UpdatedBy = actorName;
        entry.RevisionNumber++;
        // An override that supplies new facts is, by definition, a fresh/reviewed one.
        entry.IsStale = false;
        entry.LastRefreshError = "";

        db.ReleaseCatalogRevisions.Add(new ReleaseCatalogRevision
        {
            ReleaseCatalogEntryId = entry.Id,
            RevisedBy = actorName,
            ChangeSummary = $"Manual override: {reason}",
            SnapshotJson = before
        });

        await db.SaveChangesAsync();
        await audit.RecordUserActionAsync("", actorName, "ReleaseCatalogEntryOverridden", "ReleaseCatalogEntry", entry.Id.ToString(), entry.ReleaseLabel, reason: reason, beforeJson: before);
        return true;
    }

    /// <summary>Re-confirms an entry against its configured source. Facts are curated manually, so this only checks configuration and expiry; on failure only the staleness warning changes.</summary>
    public async Task<ReleaseCatalogRefreshOutcome> RefreshAsync(int entryId, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        var entry = await db.ReleaseCatalogEntries.FirstOrDefaultAsync(c => c.Id == entryId);
        if (entry is null)
        {
            return new ReleaseCatalogRefreshOutcome(false, "Entry not found.");
        }

        var outcome = ReleaseCatalogRefreshEvaluator.Evaluate(entry);
        entry.LastRefreshAttemptUtc = DateTime.UtcNow;

        if (outcome.Succeeded)
        {
            entry.IsStale = false;
            entry.LastRefreshError = "";
            entry.ReviewedAtUtc = DateTime.UtcNow;
            entry.ReviewedBy = actorName;
        }
        else
        {
            entry.IsStale = true;
            entry.LastRefreshError = outcome.Error;
        }

        await db.SaveChangesAsync();
        await audit.RecordUserActionAsync("", actorName, outcome.Succeeded ? "ReleaseCatalogEntryRefreshed" : "ReleaseCatalogEntryRefreshFailed",
            "ReleaseCatalogEntry", entry.Id.ToString(), entry.ReleaseLabel, reason: outcome.Error);
        return outcome;
    }

    public async Task<List<ReleaseCatalogRevision>> ListRevisionsAsync(int entryId)
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.ReleaseCatalogRevisions.AsNoTracking()
            .Where(r => r.ReleaseCatalogEntryId == entryId)
            .OrderByDescending(r => r.RevisedAtUtc)
            .ToListAsync();
    }

    /// <summary>Revision history for every entry, newest first, keyed by entry id.</summary>
    public async Task<Dictionary<int, List<ReleaseCatalogRevision>>> ListAllRevisionsAsync()
    {
        using var db = await factory.CreateDbContextAsync();
        var all = await db.ReleaseCatalogRevisions.AsNoTracking().OrderByDescending(r => r.RevisedAtUtc).ToListAsync();
        return all.GroupBy(r => r.ReleaseCatalogEntryId).ToDictionary(g => g.Key, g => g.ToList());
    }

    /// <summary>Applies the Create and Update rows of a catalogue import preview. Each change writes a revision and an
    /// audit event. Updates replace vendor facts only; an entry that gained an active override since the preview is left
    /// alone, and a create whose key now exists is skipped, so applying twice can't duplicate.</summary>
    public async Task<(int Created, int Updated, int Skipped)> ApplyImportAsync(IEnumerable<LifecycleImportRow<ReleaseCatalogEntry>> rows, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        var now = DateTime.UtcNow;
        var existing = await db.ReleaseCatalogEntries.ToListAsync();
        var keys = existing.Select(e => ReleaseCatalogCsvImporter.Key(e.Engine, e.Family, e.ReleaseLabel)).ToHashSet();
        var created = new List<ReleaseCatalogEntry>();
        var updated = new List<(ReleaseCatalogEntry Entry, string Before)>();
        var skipped = 0;

        foreach (var row in rows)
        {
            if (row.Item is null)
            {
                continue;
            }

            if (row.Plan == ImportRowPlan.Create)
            {
                var key = ReleaseCatalogCsvImporter.Key(row.Item.Engine, row.Item.Family, row.Item.ReleaseLabel);
                if (!keys.Add(key))
                {
                    skipped++;
                    continue;
                }
                var entry = new ReleaseCatalogEntry();
                ReleaseCatalogCsvImporter.CopyImportedFields(row.Item, entry);
                entry.CreatedAtUtc = now;
                entry.UpdatedAtUtc = now;
                entry.UpdatedBy = actorName;
                entry.ReviewedAtUtc = now;
                entry.ReviewedBy = actorName;
                entry.RevisionNumber = 1;
                db.ReleaseCatalogEntries.Add(entry);
                created.Add(entry);
            }
            else if (row.Plan == ImportRowPlan.Update && row.MatchedExistingId is { } id)
            {
                var stored = existing.FirstOrDefault(e => e.Id == id);
                if (stored is null || (stored.IsManualOverride && !ReleaseCatalogMatcher.IsOverrideExpired(stored, now)))
                {
                    skipped++;
                    continue;
                }
                var before = Snapshot(stored);
                ReleaseCatalogCsvImporter.CopyImportedFields(row.Item, stored);
                if (stored.IsManualOverride)
                {
                    stored.IsManualOverride = false;
                    stored.OverrideReason = "";
                    stored.OverrideReviewedBy = "";
                    stored.OverrideExpiresUtc = null;
                }
                stored.IsStale = false;
                stored.LastRefreshError = "";
                stored.ReviewedAtUtc = now;
                stored.ReviewedBy = actorName;
                stored.UpdatedAtUtc = now;
                stored.UpdatedBy = actorName;
                stored.RevisionNumber++;
                updated.Add((stored, before));
            }
        }

        await db.SaveChangesAsync();

        foreach (var entry in created)
        {
            db.ReleaseCatalogRevisions.Add(new ReleaseCatalogRevision { ReleaseCatalogEntryId = entry.Id, RevisedBy = actorName, RevisedAtUtc = now, ChangeSummary = "Created by CSV import", SnapshotJson = "{}" });
        }
        foreach (var (entry, before) in updated)
        {
            db.ReleaseCatalogRevisions.Add(new ReleaseCatalogRevision { ReleaseCatalogEntryId = entry.Id, RevisedBy = actorName, RevisedAtUtc = now, ChangeSummary = "Updated by CSV import", SnapshotJson = before });
        }
        await db.SaveChangesAsync();

        foreach (var entry in created)
        {
            await audit.RecordUserActionAsync("", actorName, "ReleaseCatalogEntryImported", "ReleaseCatalogEntry", entry.Id.ToString(), entry.ReleaseLabel, reason: "Created by CSV import");
        }
        foreach (var (entry, before) in updated)
        {
            await audit.RecordUserActionAsync("", actorName, "ReleaseCatalogEntryImported", "ReleaseCatalogEntry", entry.Id.ToString(), entry.ReleaseLabel,
                reason: "Updated by CSV import", beforeJson: before);
        }

        return (created.Count, updated.Count, skipped);
    }

    private static string Snapshot(ReleaseCatalogEntry entry) => JsonSerializer.Serialize(new
    {
        entry.ReleaseLabel,
        entry.Major,
        entry.Minor,
        entry.Build,
        entry.Channel,
        entry.PatchTrain,
        entry.Edition,
        entry.Distribution,
        entry.ApplicabilityNotes,
        entry.GaDateUtc,
        entry.GaDatePrecision,
        entry.MainstreamSupportEndUtc,
        entry.MainstreamSupportEndPrecision,
        entry.ExtendedSupportEndUtc,
        entry.ExtendedSupportEndPrecision,
        entry.EsuSupportEndUtc,
        entry.EsuSupportEndPrecision,
        entry.DatesAreTbd,
        entry.DatesConflict,
        entry.DateTimezone,
        entry.SourceUrl,
        entry.SourceReference,
        entry.ReleaseNotesUrl,
        entry.RetrievedAtUtc,
        entry.IsManualOverride,
        entry.OverrideReason,
        entry.OverrideReviewedBy,
        entry.OverrideExpiresUtc
    });
}
