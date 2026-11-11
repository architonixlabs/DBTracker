using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.Lifecycle;

public record LifecycleWorkspaceView(
    IReadOnlyList<LifecycleWorkspaceRow> AllRows,
    IReadOnlyList<LifecycleWorkspaceRow> MatchingRows,
    LifecycleWorkspaceSummary Summary);

/// <summary>Loads lifecycle workspace rows, one per non-archived instance (cluster nodes counted, not expanded), computed fresh on each call.</summary>
/// <remarks>The workspace and report builder get the same AllRows/MatchingRows, so an export matches the screen.</remarks>
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
        var dependencyCounts = await db.InstanceDependencies.AsNoTracking()
            .Where(d => d.Kind == DependencyKind.Deployment || d.Kind == DependencyKind.HostedDatabase)
            .GroupBy(d => new { d.DatabaseInstanceId, d.Kind })
            .Select(g => new { g.Key.DatabaseInstanceId, g.Key.Kind, Count = g.Count() })
            .ToListAsync();
        // A window whose planned end has passed while it is still Planned, Approved or InProgress makes its campaign late.
        var overdueWindowCounts = await db.ChangeWindows.AsNoTracking()
            .Where(w => w.PlannedEndUtc < now &&
                        (w.Status == ChangeWindowStatus.Planned || w.Status == ChangeWindowStatus.Approved || w.Status == ChangeWindowStatus.InProgress))
            .GroupBy(w => w.MigrationCampaignId)
            .Select(g => new { CampaignId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.CampaignId, g => g.Count);

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
                expiringCount,
                IdentityMajor: identity?.Major,
                DeploymentCount: dependencyCounts.Where(d => d.DatabaseInstanceId == instance.Id && d.Kind == DependencyKind.Deployment).Sum(d => d.Count),
                HostedDatabaseCount: dependencyCounts.Where(d => d.DatabaseInstanceId == instance.Id && d.Kind == DependencyKind.HostedDatabase).Sum(d => d.Count),
                OverdueChangeWindowCount: latestCampaign is not null && overdueWindowCounts.TryGetValue(latestCampaign.Id, out var overdue) ? overdue : 0));
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
