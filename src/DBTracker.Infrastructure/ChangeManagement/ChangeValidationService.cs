using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.ChangeManagement;

/// <summary>
/// D13 — post-change validation and sign-off per instance within a D10 MigrationCampaign. Recording
/// a validation or a sign-off here never itself completes the campaign — CampaignCompletionGateService
/// (via the pure ChangeCompletionGateEvaluator) is the only thing CampaignService.TransitionAsync
/// consults before allowing CampaignState.Completed.
/// </summary>
public class ChangeValidationService(IDbContextFactory<DBTrackerDbContext> factory, AuditRecorder audit)
{
    public async Task<List<ChangeValidation>> ListForCampaignAsync(int campaignId)
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.ChangeValidations.AsNoTracking()
            .Include(v => v.Signoffs)
            .Include(v => v.DatabaseInstance)
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

    public async Task<ChangeValidation> RecordAsync(ChangeValidation validation, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        validation.RecordedAtUtc = DateTime.UtcNow;
        validation.RecordedBy = actorName;
        validation.UpdatedAtUtc = DateTime.UtcNow;
        db.ChangeValidations.Add(validation);
        await db.SaveChangesAsync();

        var instance = await db.DatabaseInstances.FirstAsync(i => i.Id == validation.DatabaseInstanceId);
        await audit.RecordUserActionAsync("", actorName, "ChangeValidationRecorded", "DatabaseInstance", validation.DatabaseInstanceId.ToString(), instance.Name,
            reason: $"Observed build \"{validation.ObservedTargetBuildText}\" — a version-text edit alone is never treated as evidence this passed.");
        return validation;
    }

    public async Task<bool> AddSignoffAsync(int validationId, ChangeValidationSignoff signoff, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        var validation = await db.ChangeValidations.Include(v => v.DatabaseInstance).FirstOrDefaultAsync(v => v.Id == validationId);
        if (validation is null)
        {
            return false;
        }

        signoff.ChangeValidationId = validationId;
        signoff.SignedAtUtc = DateTime.UtcNow;
        db.ChangeValidationSignoffs.Add(signoff);
        await db.SaveChangesAsync();

        await audit.RecordUserActionAsync("", actorName, "ChangeValidationSignoffRecorded", "DatabaseInstance", validation.DatabaseInstanceId.ToString(), validation.DatabaseInstance?.Name ?? "",
            reason: $"{signoff.ReviewerScope}: {signoff.Outcome} by {signoff.ReviewerName}");
        return true;
    }
}
