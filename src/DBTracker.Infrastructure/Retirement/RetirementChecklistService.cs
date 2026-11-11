using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using DBTracker.Infrastructure.Audit;
using DBTracker.Infrastructure.Lifecycle;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.Retirement;

public record RetirementChecklistView(
    DatabaseInstance Instance,
    RetirementChecklist Checklist,
    RetirementChecklistEvaluation Evaluation,
    bool HasActiveException,
    bool CanRetire);

/// <summary>Retirement checklists. EvaluateAsync gathers the evidence and RetirementChecklistGateEvaluator decides.</summary>
/// <remarks>CompleteRetirementAsync is the only path that retires an instance, and it does so through InstanceArchiveService.ArchiveAsync, never by writing IsArchived.</remarks>
public class RetirementChecklistService(
    IDbContextFactory<DBTrackerDbContext> factory,
    AuditRecorder audit,
    InstanceArchiveService archiveService)
{
    private const int CopyStaleAfterDays = 30;

    /// <summary>The instance's open (not yet retired) checklist, or null. Viewing never creates one, because an open checklist blocks a plain archive.</summary>
    public async Task<RetirementChecklist?> GetOpenForInstanceAsync(int instanceId)
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.RetirementChecklists.AsNoTracking()
            .Where(c => c.DatabaseInstanceId == instanceId && c.RetiredAtUtc == null && c.CancelledAtUtc == null)
            .OrderByDescending(c => c.CreatedAtUtc)
            .FirstOrDefaultAsync();
    }

    /// <summary>Starts a checklist, or returns the open one. A checklist from an earlier, completed retirement is never reused.</summary>
    public async Task<RetirementChecklist> GetOrCreateForInstanceAsync(int instanceId, int? migrationCampaignId, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        var existing = await db.RetirementChecklists
            .Where(c => c.DatabaseInstanceId == instanceId && c.RetiredAtUtc == null && c.CancelledAtUtc == null)
            .OrderByDescending(c => c.CreatedAtUtc)
            .FirstOrDefaultAsync();
        if (existing is not null)
        {
            return existing;
        }

        var instance = await db.DatabaseInstances.FirstAsync(i => i.Id == instanceId);
        var checklist = new RetirementChecklist
        {
            DatabaseInstanceId = instanceId,
            MigrationCampaignId = migrationCampaignId,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            CreatedBy = actorName,
            UpdatedBy = actorName
        };
        db.RetirementChecklists.Add(checklist);
        await db.SaveChangesAsync();

        await audit.RecordUserActionAsync("", actorName, "RetirementChecklistCreated", "DatabaseInstance", instanceId.ToString(), instance.Name);
        return checklist;
    }

    public async Task<RetirementChecklistView?> GetViewAsync(int checklistId)
    {
        using var db = await factory.CreateDbContextAsync();
        var checklist = await db.RetirementChecklists.AsNoTracking().FirstOrDefaultAsync(c => c.Id == checklistId);
        if (checklist is null)
        {
            return null;
        }

        var instance = await db.DatabaseInstances.AsNoTracking().FirstOrDefaultAsync(i => i.Id == checklist.DatabaseInstanceId);
        if (instance is null)
        {
            return null;
        }

        var now = DateTime.UtcNow;

        var dependencies = await db.InstanceDependencies.AsNoTracking()
            .Where(d => d.DatabaseInstanceId == checklist.DatabaseInstanceId)
            .ToListAsync();

        var chainRequirements = await db.BackupChainRequirements.AsNoTracking()
            .Where(r => r.DatabaseInstanceId == checklist.DatabaseInstanceId)
            .ToListAsync();
        var backupRecords = await db.BackupRecords.AsNoTracking()
            .Where(r => r.DatabaseInstanceId == checklist.DatabaseInstanceId)
            .ToListAsync();
        ChainCoverageStatus? chainCoverageStatus = null;
        if (chainRequirements.Count > 0)
        {
            chainCoverageStatus = BackupChainCoverageCalculator.Compute(chainRequirements, backupRecords, now, instance.ExpectedBackupIntervalHours).Status;
        }

        var copyLocations = await db.BackupCopyLocations.AsNoTracking()
            .Where(c => c.DatabaseInstanceId == checklist.DatabaseInstanceId)
            .ToListAsync();
        var hasAnyCopyLocations = copyLocations.Count > 0;
        var hasAccessibleVerifiedCopy = copyLocations.Any(c => BackupCopyAvailabilityEvaluator.Evaluate(c, now, CopyStaleAfterDays).IsCurrentlyVerifiedAvailable);

        var certificates = await db.CertificateDependencies.AsNoTracking()
            .Where(c => c.DatabaseInstanceId == checklist.DatabaseInstanceId)
            .ToListAsync();
        bool? certificateAccessVerified = certificates.Count == 0 ? null : certificates.All(c => c.VerifiedBackupRestoreAccess);

        // Without a linked campaign the latest validation from any campaign governs, so an unlinked checklist can't skip an open rollback window.
        var validations = db.ChangeValidations.AsNoTracking().Where(v => v.DatabaseInstanceId == checklist.DatabaseInstanceId);
        if (checklist.MigrationCampaignId is { } campaignId)
        {
            validations = validations.Where(v => v.MigrationCampaignId == campaignId);
        }
        var hasChangeValidationHistory = await validations.AnyAsync();
        var rollbackDeadline = await validations
            .OrderByDescending(v => v.RecordedAtUtc)
            .Select(v => v.RollbackRetentionDeadlineUtc)
            .FirstOrDefaultAsync();

        var evaluation = RetirementChecklistGateEvaluator.Evaluate(
            checklist, dependencies, chainCoverageStatus, hasAccessibleVerifiedCopy, hasAnyCopyLocations, certificateAccessVerified, rollbackDeadline, now,
            hasChangeValidationHistory);

        var hasActiveException = (await db.ReadinessExceptions.AsNoTracking()
                .Where(e => e.TargetType == "RetirementChecklist" && e.TargetId == checklistId)
                .ToListAsync())
            .Any(e => ReadinessExceptionEvaluator.IsActive(e, now));

        var canRetire = evaluation.AllGatesSatisfied || hasActiveException;

        return new RetirementChecklistView(instance, checklist, evaluation, hasActiveException, canRetire);
    }

    public async Task UpdateManualFieldsAsync(int checklistId,
        bool legalHoldReviewed, string legalHoldNotes,
        string externalCleanupNotes,
        bool costContractReviewed, string costContractNotes,
        string finalBackupEvidenceNotes,
        string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        var checklist = await db.RetirementChecklists.FirstOrDefaultAsync(c => c.Id == checklistId);
        if (checklist is null)
        {
            return;
        }

        checklist.LegalHoldReviewed = legalHoldReviewed;
        checklist.LegalHoldReviewNotes = legalHoldNotes;
        checklist.ExternalCleanupNotes = externalCleanupNotes;
        checklist.CostContractReviewed = costContractReviewed;
        checklist.CostContractReviewNotes = costContractNotes;
        checklist.FinalBackupEvidenceNotes = finalBackupEvidenceNotes;
        checklist.UpdatedAtUtc = DateTime.UtcNow;
        checklist.UpdatedBy = actorName;
        await db.SaveChangesAsync();

        var instance = await db.DatabaseInstances.FirstAsync(i => i.Id == checklist.DatabaseInstanceId);
        await audit.RecordUserActionAsync("", actorName, "RetirementChecklistUpdated", "DatabaseInstance", checklist.DatabaseInstanceId.ToString(), instance.Name);
    }

    public async Task RecordSignoffAsync(int checklistId, bool isApplicationOwner, string signedBy, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        var checklist = await db.RetirementChecklists.FirstOrDefaultAsync(c => c.Id == checklistId);
        if (checklist is null)
        {
            return;
        }

        if (isApplicationOwner)
        {
            checklist.AppOwnerSignoffBy = signedBy;
            checklist.AppOwnerSignoffAtUtc = DateTime.UtcNow;
        }
        else
        {
            checklist.PlatformSignoffBy = signedBy;
            checklist.PlatformSignoffAtUtc = DateTime.UtcNow;
        }
        checklist.UpdatedAtUtc = DateTime.UtcNow;
        checklist.UpdatedBy = actorName;
        await db.SaveChangesAsync();

        var instance = await db.DatabaseInstances.FirstAsync(i => i.Id == checklist.DatabaseInstanceId);
        await audit.RecordUserActionAsync("", actorName, "RetirementChecklistSignoffRecorded", "DatabaseInstance", checklist.DatabaseInstanceId.ToString(), instance.Name,
            reason: $"{(isApplicationOwner ? "Application owner" : "Platform")} sign-off by {signedBy}");
    }

    /// <summary>Abandons an open checklist (started by mistake, or the database is staying). The row and its sign-offs are kept for history.</summary>
    public async Task<(bool Ok, string Reason)> CancelAsync(int checklistId, string reason, string actorId, string actorName)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return (false, "A reason is required to cancel a retirement checklist.");
        }

        using var db = await factory.CreateDbContextAsync();
        var checklist = await db.RetirementChecklists.FirstOrDefaultAsync(c => c.Id == checklistId);
        if (checklist is null || checklist.RetiredAtUtc is not null || checklist.CancelledAtUtc is not null)
        {
            return (false, "Only an open retirement checklist can be cancelled.");
        }

        var now = DateTime.UtcNow;
        checklist.CancelledAtUtc = now;
        checklist.CancelledBy = actorName;
        checklist.CancelReason = reason.Trim();
        checklist.UpdatedAtUtc = now;
        checklist.UpdatedBy = actorName;
        await db.SaveChangesAsync();

        var instance = await db.DatabaseInstances.FirstAsync(i => i.Id == checklist.DatabaseInstanceId);
        await audit.RecordUserActionAsync(actorId, actorName, "RetirementChecklistCancelled", "DatabaseInstance", checklist.DatabaseInstanceId.ToString(), instance.Name,
            reason: $"Checklist #{checklistId}: {checklist.CancelReason}");
        return (true, "");
    }

    /// <summary>Re-evaluates every gate and refuses unless all pass or an active scoped exception exists. On success, archives through InstanceArchiveService and stamps RetiredAtUtc/RetiredBy.</summary>
    public async Task<(bool Ok, string Reason)> CompleteRetirementAsync(int checklistId, string reason, string actorId, string actorName)
    {
        var view = await GetViewAsync(checklistId);
        if (view is null)
        {
            return (false, "Retirement checklist not found.");
        }

        if (view.Checklist.CancelledAtUtc is not null || view.Checklist.RetiredAtUtc is not null)
        {
            return (false, "This retirement checklist is no longer open.");
        }

        if (!view.CanRetire)
        {
            return (false, "Blocked: " + string.Join("; ", view.Evaluation.BlockingReasons));
        }

        var archived = await archiveService.ArchiveForRetirementAsync(view.Instance.Id, checklistId, reason, actorId, actorName);
        if (!archived.Ok)
        {
            return (false, archived.Message);
        }

        using var db = await factory.CreateDbContextAsync();
        var checklist = await db.RetirementChecklists.FirstAsync(c => c.Id == checklistId);
        checklist.RetiredAtUtc = DateTime.UtcNow;
        checklist.RetiredBy = actorName;
        checklist.UpdatedAtUtc = DateTime.UtcNow;
        checklist.UpdatedBy = actorName;
        await db.SaveChangesAsync();

        await audit.RecordUserActionAsync(actorId, actorName, "RetirementCompleted", "DatabaseInstance", view.Instance.Id.ToString(), view.Instance.Name,
            reason: view.HasActiveException ? $"Completed under a scoped D14 exception. {reason}" : reason);
        return (true, "");
    }
}
