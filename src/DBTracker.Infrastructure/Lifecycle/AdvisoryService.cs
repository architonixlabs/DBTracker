using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using DBTracker.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.Lifecycle;

/// <summary>Imported advisories and per-instance assessments. Advisories are recorded, not scanned for; automatic evaluation never touches a reviewed assessment, and only a reviewer sets Confirmed.</summary>
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

    /// <summary>The instance's assessments in priority order (severity, service criticality, exposure and applicability together).</summary>
    public async Task<List<AdvisoryInstanceAssessment>> ListAssessmentsForInstanceAsync(int instanceId) =>
        (await ListRankedAssessmentsForInstanceAsync(instanceId)).Select(r => r.Assessment).ToList();

    public async Task<IReadOnlyList<RankedAdvisoryAssessment>> ListRankedAssessmentsForInstanceAsync(int instanceId)
    {
        using var db = await factory.CreateDbContextAsync();
        var instance = await db.DatabaseInstances.AsNoTracking().FirstOrDefaultAsync(i => i.Id == instanceId);
        if (instance is null)
        {
            return [];
        }
        var assessments = await db.AdvisoryInstanceAssessments.AsNoTracking()
            .Include(a => a.AdvisoryRecord)
            .Where(a => a.DatabaseInstanceId == instanceId)
            .ToListAsync();
        return AdvisoryPriorityRanker.Rank(assessments, instance);
    }

    /// <summary>Applies the Create and Update rows of an advisory import preview, with one audit event per advisory. A
    /// create whose key now exists is skipped, so applying twice can't duplicate. Assessments are not touched.</summary>
    public async Task<(int Created, int Updated, int Skipped)> ApplyImportAsync(IEnumerable<LifecycleImportRow<AdvisoryRecord>> rows, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        var now = DateTime.UtcNow;
        var existing = await db.AdvisoryRecords.ToListAsync();
        var keys = existing.Select(a => AdvisoryCsvImporter.Key(a.Engine, a.Family, a.VendorAdvisoryId)).ToHashSet();
        var created = new List<AdvisoryRecord>();
        var updated = new List<AdvisoryRecord>();
        var skipped = 0;

        foreach (var row in rows)
        {
            if (row.Item is null)
            {
                continue;
            }
            if (row.Plan == ImportRowPlan.Create)
            {
                if (!keys.Add(AdvisoryCsvImporter.Key(row.Item.Engine, row.Item.Family, row.Item.VendorAdvisoryId)))
                {
                    skipped++;
                    continue;
                }
                var advisory = new AdvisoryRecord { ImportedAtUtc = now, ImportedBy = actorName };
                AdvisoryCsvImporter.CopyImportedFields(row.Item, advisory);
                db.AdvisoryRecords.Add(advisory);
                created.Add(advisory);
            }
            else if (row.Plan == ImportRowPlan.Update && row.MatchedExistingId is { } id && existing.FirstOrDefault(a => a.Id == id) is { } stored)
            {
                AdvisoryCsvImporter.CopyImportedFields(row.Item, stored);
                stored.ImportedAtUtc = now;
                stored.ImportedBy = actorName;
                updated.Add(stored);
            }
        }

        await db.SaveChangesAsync();
        foreach (var a in created)
        {
            await audit.RecordUserActionAsync("", actorName, "AdvisoryImported", "AdvisoryRecord", a.Id.ToString(), a.VendorAdvisoryId, reason: "Created by CSV import");
        }
        foreach (var a in updated)
        {
            await audit.RecordUserActionAsync("", actorName, "AdvisoryImported", "AdvisoryRecord", a.Id.ToString(), a.VendorAdvisoryId, reason: "Updated by CSV import");
        }
        return (created.Count, updated.Count, skipped);
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

    /// <summary>A reviewer's judgment. The only path to Confirmed, and it stops automatic re-evaluation of this assessment.</summary>
    /// <remarks>Confirmed needs evidence and an owner, plus a remediation deadline unless the fix is already verified, so
    /// a confirmed exposure always has someone accountable and a date.</remarks>
    public async Task<(bool Ok, string Error)> ReviewAssessmentAsync(int assessmentId, AdvisoryClassification classification, string reviewerEvidence,
        string owner, DateTime? remediationDeadlineUtc, string targetPatch, AdvisoryVerificationStatus verificationStatus, string reviewedBy,
        string verificationNotes = "")
    {
        var error = AdvisoryReviewValidator.Validate(classification, reviewerEvidence, owner, remediationDeadlineUtc, verificationStatus);
        if (error is not null)
        {
            return (false, error);
        }

        using var db = await factory.CreateDbContextAsync();
        var assessment = await db.AdvisoryInstanceAssessments.Include(a => a.DatabaseInstance).FirstOrDefaultAsync(a => a.Id == assessmentId);
        if (assessment is null)
        {
            return (false, "Assessment not found.");
        }

        assessment.Classification = classification;
        assessment.ReviewerEvidence = reviewerEvidence;
        assessment.Owner = owner;
        assessment.RemediationDeadlineUtc = remediationDeadlineUtc;
        assessment.TargetPatch = targetPatch;
        assessment.VerificationStatus = verificationStatus;
        assessment.VerificationNotes = verificationNotes;
        assessment.IsReviewed = true;
        assessment.ReviewedBy = reviewedBy;
        assessment.ReviewedAtUtc = DateTime.UtcNow;
        assessment.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await audit.RecordUserActionAsync("", reviewedBy, "AdvisoryAssessmentReviewed", "DatabaseInstance",
            assessment.DatabaseInstanceId.ToString(), assessment.DatabaseInstance?.Name ?? "",
            reason: $"Classification: {classification}; verification {verificationStatus}" +
                    (string.IsNullOrWhiteSpace(owner) ? "" : $"; owner {owner}") +
                    (remediationDeadlineUtc is { } due ? $"; deadline {due:yyyy-MM-dd}" : "") +
                    (string.IsNullOrWhiteSpace(targetPatch) ? "" : $"; target patch {targetPatch}"));
        return (true, "");
    }
}
