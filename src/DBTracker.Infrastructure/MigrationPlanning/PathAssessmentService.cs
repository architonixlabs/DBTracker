using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using DBTracker.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.MigrationPlanning;

/// <summary>Curated path rules and per-instance assessments. EvaluateAsync stores the classifier's result as-is, so no match stays Unknown.</summary>
public class PathAssessmentService(IDbContextFactory<DBTrackerDbContext> factory, AuditRecorder audit)
{
    public async Task<List<PathSupportRule>> ListRulesAsync(DatabaseEngine? engine = null)
    {
        using var db = await factory.CreateDbContextAsync();
        var query = db.PathSupportRules.AsNoTracking().AsQueryable();
        if (engine is { } e)
        {
            query = query.Where(r => r.Engine == e);
        }
        return await query.OrderBy(r => r.Engine).ThenBy(r => r.SourceFamily).ThenBy(r => r.TargetFamily).ToListAsync();
    }

    public async Task<PathSupportRule> CreateRuleAsync(PathSupportRule rule, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        rule.CreatedAtUtc = DateTime.UtcNow;
        rule.UpdatedAtUtc = DateTime.UtcNow;
        db.PathSupportRules.Add(rule);
        await db.SaveChangesAsync();

        await audit.RecordUserActionAsync("", actorName, "PathSupportRuleCreated", "PathSupportRule", rule.Id.ToString(),
            $"{rule.SourceFamily} -> {rule.TargetFamily} ({rule.AssessmentType}/{rule.Method}): {rule.Status}");
        return rule;
    }

    public async Task<List<PathAssessment>> ListForInstanceAsync(int instanceId)
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.PathAssessments.AsNoTracking()
            .Include(a => a.MatchedRule)
            .Where(a => a.DatabaseInstanceId == instanceId)
            .OrderByDescending(a => a.EvaluatedAtUtc)
            .ToListAsync();
    }

    /// <summary>Evaluates a source/target pairing against the curated rule set and persists the result (Supported/Blocked/Unknown).</summary>
    public async Task<PathAssessment> EvaluateAsync(
        int instanceId, MigrationAssessmentType assessmentType, MigrationMethod method,
        string sourceProduct, string sourceBuild, string sourceEdition, string sourcePlatform, string sourceTopology,
        string targetProduct, string targetBuild, string targetEdition, string targetPlatform, string targetTopology,
        string vendorGuidanceUrl, string vendorGuidanceNotes, string requiredIntermediateVersions, string downgradeConstraints,
        string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        var instance = await db.DatabaseInstances.FirstAsync(i => i.Id == instanceId);
        var rules = await db.PathSupportRules.Where(r => r.Engine == instance.Engine).ToListAsync();

        var result = PathSupportClassifier.Classify(instance.Engine, assessmentType, method,
            sourceProduct, sourceBuild, sourceEdition, sourcePlatform, sourceTopology,
            targetProduct, targetBuild, targetEdition, targetPlatform, targetTopology, rules);

        var assessment = new PathAssessment
        {
            DatabaseInstanceId = instanceId,
            AssessmentType = assessmentType,
            Method = method,
            SourceProduct = sourceProduct, SourceBuild = sourceBuild, SourceEdition = sourceEdition, SourcePlatform = sourcePlatform, SourceTopology = sourceTopology,
            TargetProduct = targetProduct, TargetBuild = targetBuild, TargetEdition = targetEdition, TargetPlatform = targetPlatform, TargetTopology = targetTopology,
            MatchedRuleId = result.MatchedRule?.Id,
            Status = result.Status,
            StatusReasoning = result.Reasoning,
            VendorGuidanceUrl = vendorGuidanceUrl,
            VendorGuidanceNotes = vendorGuidanceNotes,
            RequiredIntermediateVersions = requiredIntermediateVersions,
            DowngradeConstraints = downgradeConstraints,
            EvaluatedAtUtc = DateTime.UtcNow,
            EvaluatedBy = actorName
        };
        db.PathAssessments.Add(assessment);
        await db.SaveChangesAsync();

        await audit.RecordUserActionAsync("", actorName, "PathAssessmentEvaluated", "DatabaseInstance", instanceId.ToString(), instance.Name,
            reason: $"{assessmentType}/{method} {sourceProduct} {sourceBuild} -> {targetProduct} {targetBuild}: {result.Status}");

        return assessment;
    }
}
