using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using DBTracker.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.Lifecycle;

/// <summary>Approved patch baselines and the per-instance comparison. The approved target is only set here, by a reviewer.</summary>
public class PatchBaselineService(IDbContextFactory<DBTrackerDbContext> factory, AuditRecorder audit, ReleaseCatalogService catalog)
{
    public async Task<List<ApprovedPatchBaseline>> ListAsync()
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.ApprovedPatchBaselines.AsNoTracking()
            .Include(b => b.TargetReleaseCatalogEntry)
            .OrderBy(b => b.Engine).ThenBy(b => b.Family)
            .ToListAsync();
    }

    public async Task<ApprovedPatchBaseline?> GetActiveAsync(DatabaseEngine engine, string family)
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.ApprovedPatchBaselines.AsNoTracking()
            .Include(b => b.TargetReleaseCatalogEntry)
            .Where(b => b.Engine == engine && b.Family == family && b.IsActive)
            .OrderByDescending(b => b.ReviewedAtUtc)
            .FirstOrDefaultAsync();
    }

    public async Task UpsertAsync(DatabaseEngine engine, string family, int targetReleaseCatalogEntryId, string reviewedBy, DateTime? expiresUtc, string notes)
    {
        using var db = await factory.CreateDbContextAsync();

        // Supersede rather than overwrite: the previous baseline is deactivated and kept for history.
        var previous = await db.ApprovedPatchBaselines.Where(b => b.Engine == engine && b.Family == family && b.IsActive).ToListAsync();
        foreach (var p in previous)
        {
            p.IsActive = false;
            p.UpdatedAtUtc = DateTime.UtcNow;
        }

        var baseline = new ApprovedPatchBaseline
        {
            Engine = engine,
            Family = family,
            TargetReleaseCatalogEntryId = targetReleaseCatalogEntryId,
            ReviewedBy = reviewedBy,
            ReviewedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = expiresUtc,
            Notes = notes
        };
        db.ApprovedPatchBaselines.Add(baseline);
        await db.SaveChangesAsync();

        await audit.RecordUserActionAsync("", reviewedBy, "ApprovedPatchBaselineSet", "ApprovedPatchBaseline", baseline.Id.ToString(), $"{engine}/{family}", reason: notes);
    }

    public async Task<PatchBaselineResult?> ComputeForInstanceAsync(int instanceId)
    {
        using var db = await factory.CreateDbContextAsync();
        var instance = await db.DatabaseInstances.AsNoTracking().FirstOrDefaultAsync(i => i.Id == instanceId);
        var identity = instance is null ? null : await db.VersionIdentities.AsNoTracking().FirstOrDefaultAsync(v => v.DatabaseInstanceId == instanceId);
        if (instance is null || identity is null)
        {
            return null;
        }

        var candidates = await catalog.FindApplicableAsync(instance.Engine, identity.Family);
        var baseline = await GetActiveAsync(instance.Engine, identity.Family);

        return PatchBaselineComparer.Compare(identity, candidates, baseline, DateTime.UtcNow);
    }
}
