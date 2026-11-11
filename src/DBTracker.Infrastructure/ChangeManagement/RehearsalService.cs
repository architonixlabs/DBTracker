using DBTracker.Core.Entities;
using DBTracker.Core.Services;
using DBTracker.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace DBTracker.Infrastructure.ChangeManagement;

/// <summary>Records rehearsals with their rollback decision points and reads back the latest one with its computed readiness.</summary>
public class RehearsalService(IDbContextFactory<DBTrackerDbContext> factory, AuditRecorder audit, IConfiguration configuration)
{
    private int DefaultValidityDays => configuration.GetValue("Rehearsal:DefaultValidityDays", RehearsalEvidenceEvaluator.DefaultValidityDays);

    public async Task<List<RehearsalRecord>> ListForInstanceAsync(int instanceId, int? campaignId = null)
    {
        using var db = await factory.CreateDbContextAsync();
        var query = db.RehearsalRecords.AsNoTracking()
            .Include(r => r.RollbackDecisionPoints)
            .Where(r => r.DatabaseInstanceId == instanceId);
        if (campaignId is { } cid)
        {
            query = query.Where(r => r.MigrationCampaignId == cid);
        }
        return await query.OrderByDescending(r => r.PerformedAtUtc).ToListAsync();
    }

    public async Task<List<RehearsalRecord>> ListForCampaignAsync(int campaignId)
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.RehearsalRecords.AsNoTracking()
            .Include(r => r.RollbackDecisionPoints)
            .Include(r => r.DatabaseInstance)
            .Where(r => r.MigrationCampaignId == campaignId)
            .OrderByDescending(r => r.PerformedAtUtc)
            .ToListAsync();
    }

    /// <summary>The readiness result for the latest rehearsal of this instance/campaign pairing, as of now.</summary>
    public async Task<RehearsalReadinessResult> GetReadinessAsync(int instanceId, int campaignId)
    {
        var latest = (await ListForInstanceAsync(instanceId, campaignId)).FirstOrDefault();
        return RehearsalEvidenceEvaluator.Evaluate(latest, DateTime.UtcNow, DefaultValidityDays);
    }

    public async Task<RehearsalRecord> RecordAsync(RehearsalRecord rehearsal, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        rehearsal.RecordedAtUtc = DateTime.UtcNow;
        rehearsal.RecordedBy = actorName;
        db.RehearsalRecords.Add(rehearsal);
        await db.SaveChangesAsync();

        var instance = await db.DatabaseInstances.FirstAsync(i => i.Id == rehearsal.DatabaseInstanceId);
        var readiness = RehearsalEvidenceEvaluator.Evaluate(rehearsal, DateTime.UtcNow, DefaultValidityDays);
        await audit.RecordUserActionAsync("", actorName, "RehearsalRecorded", "DatabaseInstance", rehearsal.DatabaseInstanceId.ToString(), instance.Name,
            reason: $"{rehearsal.SourceLabel} -> {rehearsal.TargetLabel}: {readiness.Status}");
        return rehearsal;
    }
}
