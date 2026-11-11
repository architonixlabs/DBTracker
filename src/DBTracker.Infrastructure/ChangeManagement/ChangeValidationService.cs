using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using DBTracker.Infrastructure.Audit;
using DBTracker.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.ChangeManagement;

public record PostChangeEvidenceCandidates(DateTime? ChangeStartedUtc, IReadOnlyList<BackupRecord> Backups, IReadOnlyList<RestoreEvent> Restores);

/// <summary>Post-change validation and sign-off per instance. Recording them never completes the campaign; only the completion gate can.</summary>
public class ChangeValidationService(IDbContextFactory<DBTrackerDbContext> factory, AuditRecorder audit)
{
    private static readonly string[] RecorderRoles = [Roles.Admin, Roles.Dba];

    public async Task<List<ChangeValidation>> ListForCampaignAsync(int campaignId)
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.ChangeValidations.AsNoTracking()
            .Include(v => v.Signoffs)
            .Include(v => v.DatabaseInstance)
            .Include(v => v.LinkedBackupRecord)
            .Include(v => v.LinkedRestoreEvent)
            .Where(v => v.MigrationCampaignId == campaignId)
            .OrderByDescending(v => v.RecordedAtUtc)
            .ToListAsync();
    }

    public async Task<ChangeValidation?> GetLatestAsync(int campaignId, int instanceId)
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.ChangeValidations.AsNoTracking()
            .Include(v => v.Signoffs)
            .Where(v => v.MigrationCampaignId == campaignId && v.DatabaseInstanceId == instanceId)
            .OrderByDescending(v => v.RecordedAtUtc)
            .FirstOrDefaultAsync();
    }

    /// <summary>When the change started: the campaign's latest move to InProgress, else the earliest window that got under way. Null when unknown.</summary>
    private static async Task<DateTime?> ChangeStartedAsync(DBTrackerDbContext db, int campaignId)
    {
        var started = await db.CampaignStateTransitions
            .Where(t => t.MigrationCampaignId == campaignId && t.ToState == CampaignState.InProgress)
            .OrderByDescending(t => t.TransitionedAtUtc)
            .Select(t => (DateTime?)t.TransitionedAtUtc)
            .FirstOrDefaultAsync();
        return started ?? await db.ChangeWindows
            .Where(w => w.MigrationCampaignId == campaignId &&
                        (w.Status == ChangeWindowStatus.InProgress || w.Status == ChangeWindowStatus.Completed || w.Status == ChangeWindowStatus.RolledBack))
            .OrderBy(w => w.PlannedStartUtc)
            .Select(w => (DateTime?)w.PlannedStartUtc)
            .FirstOrDefaultAsync();
    }

    /// <summary>Successful backups and restores for the instance since the change started, newest first, for the evidence pickers.</summary>
    public async Task<PostChangeEvidenceCandidates> ListEvidenceCandidatesAsync(int campaignId, int instanceId)
    {
        using var db = await factory.CreateDbContextAsync();
        var started = await ChangeStartedAsync(db, campaignId);
        var backups = await db.BackupRecords.AsNoTracking()
            .Where(b => b.DatabaseInstanceId == instanceId && b.Status == BackupStatus.Success && (started == null || b.TimestampUtc >= started))
            .OrderByDescending(b => b.TimestampUtc)
            .Take(20)
            .ToListAsync();
        var restores = await db.RestoreEvents.AsNoTracking()
            .Where(r => r.DatabaseInstanceId == instanceId && r.Outcome == RestoreOutcome.Success && (started == null || r.TimestampUtc >= started))
            .OrderByDescending(r => r.TimestampUtc)
            .Take(20)
            .ToListAsync();
        return new PostChangeEvidenceCandidates(started, backups, restores);
    }

    /// <summary>Records a validation with its linked backup/restore evidence checked, and keeps before/after snapshots: the inventory's recorded
    /// version and identity at this moment, and what the reviewer observed.</summary>
    public async Task<(bool Ok, string Reason, ChangeValidation? Validation)> RecordAsync(ChangeValidation validation, string actorName, IReadOnlyCollection<string> actorRoles)
    {
        if (!actorRoles.Any(RecorderRoles.Contains))
        {
            return (false, "Only Admin and DBA users can record a post-change validation.", null);
        }

        using var db = await factory.CreateDbContextAsync();
        var instance = await db.DatabaseInstances.AsNoTracking().FirstOrDefaultAsync(i => i.Id == validation.DatabaseInstanceId);
        var linked = await db.CampaignInstanceLinks.AnyAsync(l => l.MigrationCampaignId == validation.MigrationCampaignId && l.DatabaseInstanceId == validation.DatabaseInstanceId);
        if (instance is null || !linked)
        {
            return (false, "That instance isn't part of this campaign.", null);
        }

        var backup = validation.LinkedBackupRecordId is { } backupId ? await db.BackupRecords.AsNoTracking().FirstOrDefaultAsync(b => b.Id == backupId) : null;
        var restore = validation.LinkedRestoreEventId is { } restoreId ? await db.RestoreEvents.AsNoTracking().FirstOrDefaultAsync(r => r.Id == restoreId) : null;
        if ((validation.LinkedBackupRecordId is not null && backup is null) || (validation.LinkedRestoreEventId is not null && restore is null))
        {
            return (false, "The linked backup or restore record no longer exists.", null);
        }

        var started = await ChangeStartedAsync(db, validation.MigrationCampaignId);
        var linkErrors = ChangeEvidenceRules.ValidateLinkedEvidence(validation.NewBackupRestoreEvidenceOutcome, validation.DatabaseInstanceId, backup, restore, started);
        if (linkErrors.Count > 0)
        {
            return (false, string.Join(" ", linkErrors), null);
        }

        var now = DateTime.UtcNow;
        var identity = await db.VersionIdentities.AsNoTracking().FirstOrDefaultAsync(v => v.DatabaseInstanceId == validation.DatabaseInstanceId);
        validation.BeforeEvidenceJson = ChangeEvidenceRules.BuildBeforeSnapshot(instance, identity, now);
        validation.AfterEvidenceJson = ChangeEvidenceRules.BuildAfterSnapshot(validation, backup, restore, now);
        validation.ValidationTimeUtc ??= now;
        validation.RecordedAtUtc = now;
        validation.RecordedBy = actorName;
        validation.UpdatedAtUtc = now;
        db.ChangeValidations.Add(validation);
        await db.SaveChangesAsync();

        var evidenceNote = backup is not null ? $" Linked backup #{backup.Id} ({backup.TimestampUtc:u})." : "";
        evidenceNote += restore is not null ? $" Linked restore #{restore.Id} ({restore.TimestampUtc:u})." : "";
        await audit.RecordUserActionAsync("", actorName, "ChangeValidationRecorded", "DatabaseInstance", validation.DatabaseInstanceId.ToString(), instance.Name,
            reason: $"Observed build \"{validation.ObservedTargetBuildText}\".{evidenceNote} A version-text edit alone is never treated as evidence this passed.");
        return (true, "", validation);
    }

    /// <summary>Adds a sign-off as the signed-in user. The scope is checked against that user's roles (same rules as change-window approvals);
    /// the reviewer name is the account's, never typed.</summary>
    public async Task<(bool Ok, string Reason)> AddSignoffAsync(int validationId, string scope, SignoffOutcome outcome, string notes,
        string reviewerUserId, string reviewerName, IReadOnlyCollection<string> reviewerRoles)
    {
        if (string.IsNullOrWhiteSpace(reviewerUserId))
        {
            return (false, "Sign in to sign off.");
        }
        if (!ChangeWindowApprovalAuthorizer.IsAuthorized(scope, reviewerRoles))
        {
            return (false, $"Your role can't sign off the \"{scope}\" scope.");
        }

        using var db = await factory.CreateDbContextAsync();
        var validation = await db.ChangeValidations.Include(v => v.DatabaseInstance).FirstOrDefaultAsync(v => v.Id == validationId);
        if (validation is null)
        {
            return (false, "Validation not found.");
        }

        var signoff = new ChangeValidationSignoff
        {
            ChangeValidationId = validationId,
            ReviewerScope = scope.Trim(),
            ReviewerName = reviewerName,
            ReviewerUserId = reviewerUserId,
            Outcome = outcome,
            Notes = notes.Trim(),
            SignedAtUtc = DateTime.UtcNow
        };
        db.ChangeValidationSignoffs.Add(signoff);
        await db.SaveChangesAsync();

        await audit.RecordUserActionAsync(reviewerUserId, reviewerName, "ChangeValidationSignoffRecorded", "DatabaseInstance", validation.DatabaseInstanceId.ToString(), validation.DatabaseInstance?.Name ?? "",
            reason: $"{signoff.ReviewerScope}: {signoff.Outcome}{(string.IsNullOrWhiteSpace(signoff.Notes) ? "" : $" ({signoff.Notes})")}");
        return (true, "");
    }
}
