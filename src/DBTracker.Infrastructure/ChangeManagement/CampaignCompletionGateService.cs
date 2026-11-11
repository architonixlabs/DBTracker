using DBTracker.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace DBTracker.Infrastructure.ChangeManagement;

public record CampaignCompletionGateView(bool CanComplete, IReadOnlyList<string> BlockingReasons, bool UsedException);

/// <summary>Loads each linked instance's latest validation, sign-offs and rehearsal and runs them through ChangeCompletionGateEvaluator; every instance must pass. An active campaign-scoped exception is the only bypass and applies to all of them.</summary>
public class CampaignCompletionGateService(IDbContextFactory<DBTrackerDbContext> factory, IConfiguration configuration)
{
    public async Task<CampaignCompletionGateView> EvaluateAsync(int campaignId)
    {
        using var db = await factory.CreateDbContextAsync();
        var now = DateTime.UtcNow;
        var defaultValidityDays = configuration.GetValue("Rehearsal:DefaultValidityDays", RehearsalEvidenceEvaluator.DefaultValidityDays);

        var instanceIds = await db.CampaignInstanceLinks.Where(l => l.MigrationCampaignId == campaignId).Select(l => l.DatabaseInstanceId).ToListAsync();
        if (instanceIds.Count == 0)
        {
            return new CampaignCompletionGateView(false, ["No instances are linked to this campaign yet."], false);
        }

        var hasActiveException = (await db.ReadinessExceptions.Where(e => e.TargetType == "MigrationCampaign" && e.TargetId == campaignId).ToListAsync())
            .Any(e => ReadinessExceptionEvaluator.IsActive(e, now));

        var reasons = new List<string>();
        var usedException = false;

        foreach (var instanceId in instanceIds)
        {
            var instanceName = (await db.DatabaseInstances.AsNoTracking().FirstAsync(i => i.Id == instanceId)).Name;

            var validation = await db.ChangeValidations.AsNoTracking().Include(v => v.Signoffs)
                .Where(v => v.MigrationCampaignId == campaignId && v.DatabaseInstanceId == instanceId)
                .OrderByDescending(v => v.RecordedAtUtc)
                .FirstOrDefaultAsync();

            var latestRehearsal = await db.RehearsalRecords.AsNoTracking()
                .Where(r => r.MigrationCampaignId == campaignId && r.DatabaseInstanceId == instanceId)
                .OrderByDescending(r => r.PerformedAtUtc)
                .FirstOrDefaultAsync();
            var rehearsalResult = RehearsalEvidenceEvaluator.Evaluate(latestRehearsal, now, defaultValidityDays);

            var result = ChangeCompletionGateEvaluator.Evaluate(validation, validation?.Signoffs.ToList() ?? [], rehearsalResult, hasActiveException);
            if (!result.CanComplete || result.UsedException)
            {
                if (result.UsedException)
                {
                    usedException = true;
                }
                reasons.AddRange(result.BlockingReasons.Select(r => $"{instanceName}: {r}"));
            }
        }

        var canComplete = reasons.Count == 0 || hasActiveException;
        return new CampaignCompletionGateView(canComplete, reasons, usedException);
    }
}
