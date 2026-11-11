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

    private static string Snapshot(ReleaseCatalogEntry entry) => JsonSerializer.Serialize(new
    {
        entry.ReleaseLabel,
        entry.Major,
        entry.Minor,
        entry.Build,
        entry.Channel,
        entry.GaDateUtc,
        entry.MainstreamSupportEndUtc,
        entry.ExtendedSupportEndUtc,
        entry.EsuSupportEndUtc,
        entry.DatesAreTbd,
        entry.DatesConflict,
        entry.SourceUrl,
        entry.IsManualOverride
    });
}
