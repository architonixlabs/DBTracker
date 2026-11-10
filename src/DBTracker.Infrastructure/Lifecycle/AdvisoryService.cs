using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using DBTracker.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.Lifecycle;

/// <summary>
/// D05 — imported/referenced advisories and their per-instance applicability assessments. This app
/// never scans for advisories itself; ImportAsync only records what was already found/reviewed
/// elsewhere (e.g. DefectDojo/Dependency-Track per org standards), deliberately not duplicating
/// that platform's own UI. EvaluateForInstanceAsync applies AdvisoryApplicabilityClassifier's
/// automatic Potential/NotApplicable/Unknown suggestion, but never touches an assessment a reviewer
/// has already reviewed (IsReviewed) — only a human can set Confirmed.
/// </summary>
public class AdvisoryService(IDbContextFactory<DBTrackerDbContext> factory, AuditRecorder audit)
{
    public async Task<List<AdvisoryRecord>> ListAsync(DatabaseEngine? engine = null)
    {
        using var db = await factory.CreateDbContextAsync();
        var query = db.AdvisoryRecords.AsNoTracking().AsQueryable();
        if (engine is { } e)
        {
            query = query.Where(a => a.Engine == e);
        }
        return await query.OrderByDescending(a => a.ImportedAtUtc).ToListAsync();
    }

    public async Task<AdvisoryRecord> ImportAsync(AdvisoryRecord advisory, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        advisory.ImportedAtUtc = DateTime.UtcNow;
        advisory.ImportedBy = actorName;
        db.AdvisoryRecords.Add(advisory);
        await db.SaveChangesAsync();

        await audit.RecordUserActionAsync("", actorName, "AdvisoryImported", "AdvisoryRecord", advisory.Id.ToString(), advisory.VendorAdvisoryId);
        return advisory;
    }

    public async Task<List<AdvisoryInstanceAssessment>> ListAssessmentsForInstanceAsync(int instanceId)
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.AdvisoryInstanceAssessments.AsNoTracking()
            .Include(a => a.AdvisoryRecord)
            .Where(a => a.DatabaseInstanceId == instanceId)
            .OrderBy(a => a.AdvisoryRecord!.Severity) // enum order is Critical,High,Medium,Low,Unknown — already most-severe-first
            .ToListAsync();
    }

    /// <summary>Runs the pure classifier against every advisory for this instance's engine/family and creates/refreshes the non-reviewed assessments. Returns how many were (re)computed.</summary>
    public async Task<int> EvaluateForInstanceAsync(int instanceId, string actorName, bool distributionProvidesBackport = false)
    {
        using var db = await factory.CreateDbContextAsync();
        var instance = await db.DatabaseInstances.FirstOrDefaultAsync(i => i.Id == instanceId);
        var identity = instance is null ? null : await db.VersionIdentities.FirstOrDefaultAsync(v => v.DatabaseInstanceId == instanceId);
        if (instance is null || identity is null)
        {
            return 0;
        }

        var advisories = await db.AdvisoryRecords.Where(a => a.Engine == instance.Engine && a.Family == identity.Family).ToListAsync();
        var existing = await db.AdvisoryInstanceAssessments.Where(a => a.DatabaseInstanceId == instanceId).ToListAsync();
        var updated = 0;

        foreach (var advisory in advisories)
        {
            var assessment = existing.FirstOrDefault(a => a.AdvisoryRecordId == advisory.Id);
            if (assessment is { IsReviewed: true })
            {
                continue;
            }

            var result = AdvisoryApplicabilityClassifier.Classify(advisory, identity, distributionProvidesBackport);
            if (assessment is null)
            {
                assessment = new AdvisoryInstanceAssessment { AdvisoryRecordId = advisory.Id, DatabaseInstanceId = instanceId, CreatedAtUtc = DateTime.UtcNow };
                db.AdvisoryInstanceAssessments.Add(assessment);
            }

            assessment.Classification = result.Classification;
            assessment.ClassificationReasoning = result.Reasoning;
            assessment.DistributionProvidesBackport = distributionProvidesBackport;
            assessment.UpdatedAtUtc = DateTime.UtcNow;
            updated++;
        }

        await db.SaveChangesAsync();
        if (updated > 0)
        {
            await audit.RecordUserActionAsync("", actorName, "AdvisoryAssessmentsEvaluated", "DatabaseInstance", instanceId.ToString(), instance.Name, reason: $"{updated} advisory(ies) evaluated");
        }
        return updated;
    }

    /// <summary>A reviewer's explicit judgment — the only path that can set Confirmed, and the only path that stops future automatic re-evaluation from overwriting this assessment.</summary>
    public async Task<bool> ReviewAssessmentAsync(int assessmentId, AdvisoryClassification classification, string reviewerEvidence,
        string owner, DateTime? remediationDeadlineUtc, string targetPatch, AdvisoryVerificationStatus verificationStatus, string reviewedBy)
    {
        using var db = await factory.CreateDbContextAsync();
        var assessment = await db.AdvisoryInstanceAssessments.Include(a => a.DatabaseInstance).FirstOrDefaultAsync(a => a.Id == assessmentId);
        if (assessment is null)
        {
            return false;
        }

        assessment.Classification = classification;
        assessment.ReviewerEvidence = reviewerEvidence;
        assessment.Owner = owner;
        assessment.RemediationDeadlineUtc = remediationDeadlineUtc;
        assessment.TargetPatch = targetPatch;
        assessment.VerificationStatus = verificationStatus;
        assessment.IsReviewed = true;
        assessment.ReviewedBy = reviewedBy;
        assessment.ReviewedAtUtc = DateTime.UtcNow;
        assessment.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await audit.RecordUserActionAsync("", reviewedBy, "AdvisoryAssessmentReviewed", "DatabaseInstance",
            assessment.DatabaseInstanceId.ToString(), assessment.DatabaseInstance?.Name ?? "", reason: $"Classification: {classification}");
        return true;
    }
}
