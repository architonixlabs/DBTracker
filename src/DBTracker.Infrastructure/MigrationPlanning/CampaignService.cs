using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using DBTracker.Infrastructure.Audit;
using DBTracker.Infrastructure.ChangeManagement;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.MigrationPlanning;

/// <summary>Migration campaigns: affected deployments, owning teams, recorded alternatives, and append-only transition and approval history.</summary>
public class CampaignService(IDbContextFactory<DBTrackerDbContext> factory, AuditRecorder audit, CampaignCompletionGateService completionGate)
{
    public async Task<List<MigrationCampaign>> ListAsync()
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.MigrationCampaigns.AsNoTracking()
            .Include(c => c.InstanceLinks)
            .Include(c => c.AccountableDbaTeam)
            .Include(c => c.AccountableAppTeam)
            .Include(c => c.AccountablePlatformTeam)
            .OrderByDescending(c => c.UpdatedAtUtc)
            .ToListAsync();
    }

    public async Task<MigrationCampaign?> GetAsync(int id)
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.MigrationCampaigns.AsNoTracking()
            .Include(c => c.InstanceLinks).ThenInclude(l => l.DatabaseInstance)
            .Include(c => c.Alternatives)
            .Include(c => c.StateTransitions)
            .Include(c => c.Approvals)
            .Include(c => c.AccountableDbaTeam)
            .Include(c => c.AccountableAppTeam)
            .Include(c => c.AccountablePlatformTeam)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<MigrationCampaign> CreateAsync(MigrationCampaign campaign, IEnumerable<int> instanceIds, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        campaign.CreatedAtUtc = DateTime.UtcNow;
        campaign.UpdatedAtUtc = DateTime.UtcNow;
        campaign.CreatedBy = actorName;
        campaign.State = CampaignState.Proposed;

        foreach (var instanceId in instanceIds.Distinct())
        {
            campaign.InstanceLinks.Add(new CampaignInstanceLink { DatabaseInstanceId = instanceId });
        }

        db.MigrationCampaigns.Add(campaign);
        await db.SaveChangesAsync();

        await audit.RecordUserActionAsync("", actorName, "MigrationCampaignCreated", "MigrationCampaign", campaign.Id.ToString(), campaign.Name,
            reason: $"{campaign.SourceLabel} -> {campaign.TargetLabel}, {campaign.InstanceLinks.Count} instance(s)");
        return campaign;
    }

    public async Task<bool> AddInstanceAsync(int campaignId, int instanceId, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        var campaign = await db.MigrationCampaigns.FirstOrDefaultAsync(c => c.Id == campaignId);
        if (campaign is null)
        {
            return false;
        }

        var exists = await db.CampaignInstanceLinks.AnyAsync(l => l.MigrationCampaignId == campaignId && l.DatabaseInstanceId == instanceId);
        if (exists)
        {
            return true;
        }

        db.CampaignInstanceLinks.Add(new CampaignInstanceLink { MigrationCampaignId = campaignId, DatabaseInstanceId = instanceId });
        campaign.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var instance = await db.DatabaseInstances.FirstAsync(i => i.Id == instanceId);
        await audit.RecordUserActionAsync("", actorName, "MigrationCampaignInstanceAdded", "MigrationCampaign", campaignId.ToString(), campaign.Name,
            reason: $"Added {instance.Name}");
        return true;
    }

    public async Task<CampaignAlternative> AddAlternativeAsync(int campaignId, CampaignAlternativeType type, string description,
        decimal? estimatedEffortHours, decimal? estimatedCost, string riskNotes, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        var alternative = new CampaignAlternative
        {
            MigrationCampaignId = campaignId,
            Type = type,
            Description = description,
            EstimatedEffortHours = estimatedEffortHours,
            EstimatedCost = estimatedCost,
            RiskNotes = riskNotes,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = actorName
        };
        db.CampaignAlternatives.Add(alternative);
        await db.SaveChangesAsync();

        var campaign = await db.MigrationCampaigns.FirstAsync(c => c.Id == campaignId);
        await audit.RecordUserActionAsync("", actorName, "MigrationCampaignAlternativeAdded", "MigrationCampaign", campaignId.ToString(), campaign.Name,
            reason: $"{type}: {description}");
        return alternative;
    }

    /// <summary>Marks one alternative chosen and the rest not chosen, keeping the comparison.</summary>
    public async Task<bool> ChooseAlternativeAsync(int campaignId, int alternativeId, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        var alternatives = await db.CampaignAlternatives.Where(a => a.MigrationCampaignId == campaignId).ToListAsync();
        var chosen = alternatives.FirstOrDefault(a => a.Id == alternativeId);
        if (chosen is null)
        {
            return false;
        }

        foreach (var alt in alternatives)
        {
            alt.IsChosen = alt.Id == alternativeId;
        }
        await db.SaveChangesAsync();

        var campaign = await db.MigrationCampaigns.FirstAsync(c => c.Id == campaignId);
        await audit.RecordUserActionAsync("", actorName, "MigrationCampaignAlternativeChosen", "MigrationCampaign", campaignId.ToString(), campaign.Name,
            reason: $"Chosen: {chosen.Type} — {chosen.Description}");
        return true;
    }

    /// <summary>The only path that changes MigrationCampaign.State. Invalid jumps are refused with a reason and nothing is written; Completed additionally requires CampaignCompletionGateService to pass.</summary>
    /// <remarks>Other transitions write only the history row, so they can't imply any change to risk or readiness.</remarks>
    public async Task<(bool Ok, string Reason)> TransitionAsync(int campaignId, CampaignState toState, string reason, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        var campaign = await db.MigrationCampaigns.FirstOrDefaultAsync(c => c.Id == campaignId);
        if (campaign is null)
        {
            return (false, "Campaign not found.");
        }

        if (!CampaignStateTransitionValidator.IsValidTransition(campaign.State, toState))
        {
            return (false, CampaignStateTransitionValidator.DescribeInvalidReason(campaign.State, toState));
        }

        if (toState == CampaignState.Completed)
        {
            var gate = await completionGate.EvaluateAsync(campaignId);
            if (!gate.CanComplete)
            {
                return (false, "Cannot complete — " + string.Join("; ", gate.BlockingReasons));
            }
        }

        var fromState = campaign.State;
        campaign.State = toState;
        campaign.UpdatedAtUtc = DateTime.UtcNow;

        db.CampaignStateTransitions.Add(new CampaignStateTransition
        {
            MigrationCampaignId = campaignId,
            FromState = fromState,
            ToState = toState,
            Reason = reason,
            TransitionedAtUtc = DateTime.UtcNow,
            TransitionedBy = actorName
        });
        await db.SaveChangesAsync();

        await audit.RecordUserActionAsync("", actorName, "MigrationCampaignStateChanged", "MigrationCampaign", campaignId.ToString(), campaign.Name,
            reason: $"{fromState} -> {toState}: {reason}");
        return (true, "");
    }

    public async Task<CampaignApproval> RecordApprovalAsync(int campaignId, string scope, CampaignApprovalDecision decision, string approverName, string notes, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        var approval = new CampaignApproval
        {
            MigrationCampaignId = campaignId,
            Scope = scope,
            Decision = decision,
            ApproverName = approverName,
            Notes = notes,
            DecidedAtUtc = DateTime.UtcNow
        };
        db.CampaignApprovals.Add(approval);
        await db.SaveChangesAsync();

        var campaign = await db.MigrationCampaigns.FirstAsync(c => c.Id == campaignId);
        await audit.RecordUserActionAsync("", actorName, "MigrationCampaignApprovalRecorded", "MigrationCampaign", campaignId.ToString(), campaign.Name,
            reason: $"{scope}: {decision} by {approverName}");
        return approval;
    }
}
