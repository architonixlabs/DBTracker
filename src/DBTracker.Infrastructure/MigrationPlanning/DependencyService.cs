using DBTracker.Core.Entities;
using DBTracker.Core.Services;
using DBTracker.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.MigrationPlanning;

/// <summary>Manually mapped dependencies per instance, with audited writes. ImpactAsync shows downstream consumers and unresolved dependencies before a shared-engine change.</summary>
public class DependencyService(IDbContextFactory<DBTrackerDbContext> factory, AuditRecorder audit)
{
    public async Task<List<InstanceDependency>> ListForInstanceAsync(int instanceId)
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.InstanceDependencies.AsNoTracking()
            .Include(d => d.OwnerTeam)
            .Where(d => d.DatabaseInstanceId == instanceId)
            .OrderBy(d => d.Direction).ThenBy(d => d.Kind).ThenBy(d => d.Name)
            .ToListAsync();
    }

    public async Task<DependencyImpactResult> AssessImpactAsync(int instanceId)
    {
        var dependencies = await ListForInstanceAsync(instanceId);
        return DependencyImpactAssessor.Assess(dependencies);
    }

    public async Task<InstanceDependency> AddAsync(InstanceDependency dependency, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        dependency.CreatedAtUtc = DateTime.UtcNow;
        dependency.UpdatedAtUtc = DateTime.UtcNow;
        dependency.UpdatedBy = actorName;
        db.InstanceDependencies.Add(dependency);
        await db.SaveChangesAsync();

        var instance = await db.DatabaseInstances.FirstAsync(i => i.Id == dependency.DatabaseInstanceId);
        await audit.RecordUserActionAsync("", actorName, "InstanceDependencyRecorded", "DatabaseInstance",
            dependency.DatabaseInstanceId.ToString(), instance.Name,
            reason: $"{dependency.Kind} / {dependency.Direction}: {dependency.Name}");
        return dependency;
    }

    public async Task<bool> SetResolvedAsync(int dependencyId, bool isResolved, string resolutionNotes, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        var dependency = await db.InstanceDependencies.FirstOrDefaultAsync(d => d.Id == dependencyId);
        if (dependency is null)
        {
            return false;
        }

        dependency.IsResolved = isResolved;
        dependency.ResolutionNotes = resolutionNotes;
        dependency.LastReviewedAtUtc = DateTime.UtcNow;
        dependency.LastReviewedBy = actorName;
        dependency.UpdatedAtUtc = DateTime.UtcNow;
        dependency.UpdatedBy = actorName;
        await db.SaveChangesAsync();

        var instance = await db.DatabaseInstances.FirstAsync(i => i.Id == dependency.DatabaseInstanceId);
        await audit.RecordUserActionAsync("", actorName, "InstanceDependencyReviewed", "DatabaseInstance",
            dependency.DatabaseInstanceId.ToString(), instance.Name,
            reason: $"{dependency.Name}: {(isResolved ? "resolved" : "marked unresolved")}");
        return true;
    }

    public async Task RemoveAsync(int dependencyId, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        var dependency = await db.InstanceDependencies.FirstOrDefaultAsync(d => d.Id == dependencyId);
        if (dependency is null)
        {
            return;
        }

        db.InstanceDependencies.Remove(dependency);
        await db.SaveChangesAsync();

        await audit.RecordUserActionAsync("", actorName, "InstanceDependencyRemoved", "DatabaseInstance",
            dependency.DatabaseInstanceId.ToString(), dependency.Name);
    }
}
