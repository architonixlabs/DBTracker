using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.Lifecycle;

public record LifecycleWorkspaceView(
    IReadOnlyList<LifecycleWorkspaceRow> AllRows,
    IReadOnlyList<LifecycleWorkspaceRow> MatchingRows,
    LifecycleWorkspaceSummary Summary);

/// <summary>
/// D15 — the I/O behind the lifecycle workspace/reports. One row per non-archived DatabaseInstance
/// (ClusterNodeVersions are counted, never expanded into extra rows), pulling together D01's identity,
/// D03's support runway, D04's approved-baseline coverage, D10's latest campaign state and D14's
/// currently-active exceptions — all of it computed fresh on every call, nothing cached. The workspace
/// and the report builder are handed the exact same AllRows/MatchingRows pair, so an export can never
/// show a different scoped result than what the workspace rendered.
/// </summary>
public class LifecycleWorkspaceService(IDbContextFactory<DBTrackerDbContext> factory)
{
    public const int ExpiringExceptionHorizonDays = 30;

    public async Task<LifecycleWorkspaceView> GetWorkspaceAsync(LifecycleWorkspaceFilter filter)
    {
        var now = DateTime.UtcNow;
        var inputs = await BuildInputsAsync(now);
        var allRows = LifecycleWorkspaceBuilder.BuildRows(inputs, now);
        var matchingRows = LifecycleWorkspaceBuilder.ApplyFilter(allRows, filter);
        var summary = LifecycleWorkspaceBuilder.Summarize(allRows, matchingRows);
        return new LifecycleWorkspaceView(allRows, matchingRows, summary);
    }

    public async Task<LifecycleReport> GetReportAsync(LifecycleWorkspaceFilter filter)
    {
        var view = await GetWorkspaceAsync(filter);
        return LifecycleReportBuilder.Build(view.AllRows, view.MatchingRows);
    }

    private async Task<List<LifecycleWorkspaceInput>> BuildInputsAsync(DateTime now)
    {
        using var db = await factory.CreateDbContextAsync();

        var instances = await db.DatabaseInstances.AsNoTracking().Include(i => i.OwnerTeam).Where(i => !i.IsArchived).ToListAsync();
        var identities = await db.VersionIdentities.AsNoTracking().ToListAsync();
        var clusterCounts = await db.ClusterNodeVersions.AsNoTracking().GroupBy(c => c.VersionIdentityId).Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
        var catalogEntries = await db.ReleaseCatalogEntries.AsNoTracking().ToListAsync();
        var baselines = await db.ApprovedPatchBaselines.AsNoTracking().Include(b => b.TargetReleaseCatalogEntry).Where(b => b.IsActive).ToListAsync();
        var campaignLinks = await db.CampaignInstanceLinks.AsNoTracking().ToListAsync();
        var campaigns = await db.MigrationCampaigns.AsNoTracking().ToListAsync();
        var activeExceptions = (await db.ReadinessExceptions.AsNoTracking().ToListAsync())
            .Where(e => ReadinessExceptionEvaluator.IsActive(e, now))
            .ToList();

        var result = new List<LifecycleWorkspaceInput>();
        foreach (var instance in instances)
        {
            var identity = identities.FirstOrDefault(v => v.DatabaseInstanceId == instance.Id);

            ReleaseCatalogEntry? matchedCatalogEntry = null;
            if (identity is { ResolutionStatus: VersionResolutionStatus.Resolved })
            {
                matchedCatalogEntry = catalogEntries
                    .Where(c => c.Engine == instance.Engine && c.Family == identity.Family && c.Major == identity.Major)
                    .OrderByDescending(c => c.Minor ?? int.MinValue)
                    .FirstOrDefault();
            }
            var runway = SupportRunwayCalculator.Compute(matchedCatalogEntry, now);

            var coverage = ComputeCoverage(identity, instance.Engine, baselines);

            var instanceCampaignIds = campaignLinks.Where(l => l.DatabaseInstanceId == instance.Id).Select(l => l.MigrationCampaignId).ToList();
            var latestCampaign = campaigns.Where(c => instanceCampaignIds.Contains(c.Id)).OrderByDescending(c => c.UpdatedAtUtc).FirstOrDefault();

            var expiringCount = activeExceptions.Count(e =>
                ((e.TargetType == "DatabaseInstance" && e.TargetId == instance.Id) ||
                 (e.TargetType == "MigrationCampaign" && instanceCampaignIds.Contains(e.TargetId))) &&
                e.ExpiresAtUtc <= now.AddDays(ExpiringExceptionHorizonDays));

            var clusterCount = identity is null ? 0 : clusterCounts.FirstOrDefault(c => c.Key == identity.Id)?.Count ?? 0;

            result.Add(new LifecycleWorkspaceInput(
                instance.Id,
                instance.Name,
                instance.Engine,
                identity?.Family,
                identity?.ResolutionStatus,
                identity?.ObservedAtUtc,
                runway,
                coverage,
                instance.Criticality,
                instance.OwnerTeam?.Name,
                clusterCount,
                latestCampaign?.State,
                expiringCount));
        }

        return result;
    }

    private static LifecycleCoverageStatus ComputeCoverage(VersionIdentity? identity, DatabaseEngine engine, IReadOnlyList<ApprovedPatchBaseline> baselines)
    {
        if (identity is not { ResolutionStatus: VersionResolutionStatus.Resolved })
        {
            return LifecycleCoverageStatus.Unknown;
        }

        var baseline = baselines.FirstOrDefault(b => b.Engine == engine && b.Family == identity.Family &&
            (b.ExpiresAtUtc is null || b.ExpiresAtUtc > DateTime.UtcNow));
        if (baseline?.TargetReleaseCatalogEntry is null)
        {
            return LifecycleCoverageStatus.NoBaselineSet;
        }

        var target = baseline.TargetReleaseCatalogEntry;
        var onBaseline = target.Major == identity.Major && (target.Minor is null || identity.Minor is null || target.Minor <= identity.Minor);
        return onBaseline ? LifecycleCoverageStatus.OnApprovedBaseline : LifecycleCoverageStatus.BehindBaseline;
    }
}
