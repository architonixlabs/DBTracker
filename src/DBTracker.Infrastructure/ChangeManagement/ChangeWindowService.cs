using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using DBTracker.Infrastructure.Audit;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.ChangeManagement;

/// <summary>Change windows and the cutover journal. Every write is a recorded fact that executes and sends nothing; conflict detection and the approval gate are pure and fed from here.</summary>
public class ChangeWindowService(IDbContextFactory<DBTrackerDbContext> factory, AuditRecorder audit)
{
    public async Task<List<ChangeWindow>> ListForCampaignAsync(int campaignId)
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.ChangeWindows.AsNoTracking()
            .Include(w => w.RunbookSteps)
            .Include(w => w.Approvals)
            .Include(w => w.JournalEntries)
            .Include(w => w.CommunicationOwnerTeam)
            .Where(w => w.MigrationCampaignId == campaignId)
            .OrderByDescending(w => w.PlannedStartUtc)
            .ToListAsync();
    }

    public async Task<ChangeWindow?> GetAsync(int id)
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.ChangeWindows.AsNoTracking()
            .Include(w => w.RunbookSteps)
            .Include(w => w.Approvals)
            .Include(w => w.JournalEntries)
            .Include(w => w.CommunicationOwnerTeam)
            .Include(w => w.EmergencyException)
            .FirstOrDefaultAsync(w => w.Id == id);
    }

    /// <summary>Read-only conflict check against other scheduled windows and existing maintenance windows on the same instances. Never blocks saving.</summary>
    public async Task<ChangeWindowConflictResult> DetectConflictsAsync(int campaignId, DateTime startUtc, DateTime endUtc, int? excludingWindowId = null)
    {
        using var db = await factory.CreateDbContextAsync();
        var instanceIds = await db.CampaignInstanceLinks.Where(l => l.MigrationCampaignId == campaignId).Select(l => l.DatabaseInstanceId).ToListAsync();

        var siblingCampaignIds = await db.CampaignInstanceLinks
            .Where(l => instanceIds.Contains(l.DatabaseInstanceId))
            .Select(l => l.MigrationCampaignId)
            .Distinct()
            .ToListAsync();

        var otherWindows = await db.ChangeWindows
            .Where(w => siblingCampaignIds.Contains(w.MigrationCampaignId) && w.Status != ChangeWindowStatus.Cancelled && w.Id != excludingWindowId)
            .Select(w => new SiblingChangeWindow(w.Id, w.Title, w.PlannedStartUtc, w.PlannedEndUtc))
            .ToListAsync();

        var maintenance = await db.DatabaseInstances
            .Where(i => instanceIds.Contains(i.Id) && i.MaintenanceUntilUtc != null)
            .Select(i => new InstanceMaintenanceWindow(i.Name, i.MaintenanceUntilUtc, i.MaintenanceReason))
            .ToListAsync();

        return ChangeWindowConflictDetector.Detect(startUtc, endUtc, otherWindows, maintenance);
    }

    public async Task<ChangeWindow> CreateAsync(ChangeWindow window, IEnumerable<string> requiredApprovalScopes, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        window.CreatedAtUtc = DateTime.UtcNow;
        window.UpdatedAtUtc = DateTime.UtcNow;
        window.CreatedBy = actorName;
        window.Status = ChangeWindowStatus.Planned;

        foreach (var scope in requiredApprovalScopes.Where(s => !string.IsNullOrWhiteSpace(s)))
        {
            window.Approvals.Add(new ChangeWindowApproval { RequiredScope = scope.Trim() });
        }

        db.ChangeWindows.Add(window);
        await db.SaveChangesAsync();

        var campaign = await db.MigrationCampaigns.FirstAsync(c => c.Id == window.MigrationCampaignId);
        await audit.RecordUserActionAsync("", actorName, "ChangeWindowPlanned", "MigrationCampaign", window.MigrationCampaignId.ToString(), campaign.Name,
            reason: $"\"{window.Title}\" {window.PlannedStartUtc:u}-{window.PlannedEndUtc:u} ({window.TimeZoneLabel}). Planning fact only, nothing executed.");
        return window;
    }

    public async Task<bool> AddRunbookStepAsync(int windowId, string description, bool isRequired, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        var window = await db.ChangeWindows.Include(w => w.RunbookSteps).FirstOrDefaultAsync(w => w.Id == windowId);
        if (window is null)
        {
            return false;
        }

        var order = window.RunbookSteps.Count == 0 ? 1 : window.RunbookSteps.Max(s => s.Order) + 1;
        db.ChangeWindowRunbookSteps.Add(new ChangeWindowRunbookStep { ChangeWindowId = windowId, Order = order, Description = description, IsRequired = isRequired });
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> AddJournalEntryAsync(int windowId, CutoverJournalEntry entry, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        var window = await db.ChangeWindows.FirstOrDefaultAsync(w => w.Id == windowId);
        if (window is null)
        {
            return false;
        }

        entry.ChangeWindowId = windowId;
        entry.RecordedBy = actorName;
        entry.RecordedAtUtc = DateTime.UtcNow;
        db.CutoverJournalEntries.Add(entry);
        await db.SaveChangesAsync();

        var campaign = await db.MigrationCampaigns.FirstAsync(c => c.Id == window.MigrationCampaignId);
        await audit.RecordUserActionAsync("", actorName, "CutoverJournalEntryRecorded", "MigrationCampaign", window.MigrationCampaignId.ToString(), campaign.Name,
            reason: $"\"{window.Title}\" ({entry.EntryType}): {entry.Description}");
        return true;
    }

    /// <summary>Records one approval decision against a required approval row, after an explicit role/scope check — returns false (nothing written) when the caller's roles don't authorize that scope.</summary>
    public async Task<(bool Ok, string Reason)> RecordApprovalAsync(int approvalId, CampaignApprovalDecision decision, string approverName, string approverUserId,
        IReadOnlyCollection<string> approverRoles, string notes, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        var approval = await db.ChangeWindowApprovals.Include(a => a.ChangeWindow).FirstOrDefaultAsync(a => a.Id == approvalId);
        if (approval is null)
        {
            return (false, "Approval row not found.");
        }

        if (!ChangeWindowApprovalAuthorizer.IsAuthorized(approval.RequiredScope, approverRoles))
        {
            return (false, $"Not authorized to approve the \"{approval.RequiredScope}\" scope.");
        }

        approval.Decision = decision;
        approval.ApproverName = approverName;
        approval.ApproverUserId = approverUserId;
        approval.Notes = notes;
        approval.DecidedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var window = approval.ChangeWindow!;
        var campaign = await db.MigrationCampaigns.FirstAsync(c => c.Id == window.MigrationCampaignId);
        await audit.RecordUserActionAsync("", actorName, "ChangeWindowApprovalRecorded", "MigrationCampaign", window.MigrationCampaignId.ToString(), campaign.Name,
            reason: $"\"{window.Title}\" {approval.RequiredScope}: {decision} by {approverName}");
        return (true, "");
    }

    /// <summary>The only path that changes ChangeWindow.Status. Approved requires ChangeWindowApprovalGate to pass; otherwise it refuses with the outstanding approvals.</summary>
    public async Task<(bool Ok, string Reason)> SetStatusAsync(int windowId, ChangeWindowStatus toStatus, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        var window = await db.ChangeWindows.Include(w => w.Approvals).FirstOrDefaultAsync(w => w.Id == windowId);
        if (window is null)
        {
            return (false, "Change window not found.");
        }

        if (toStatus == ChangeWindowStatus.Approved || toStatus == ChangeWindowStatus.InProgress)
        {
            var hasActiveException = window.EmergencyExceptionId is { } exId &&
                ReadinessExceptionEvaluator.IsActive(await db.ReadinessExceptions.FirstAsync(e => e.Id == exId), DateTime.UtcNow);

            var gate = ChangeWindowApprovalGate.Evaluate(window.Approvals.ToList(), hasActiveException);
            if (!gate.CanProceed)
            {
                return (false, "Outstanding approvals: " + string.Join("; ", gate.OutstandingApprovals));
            }
        }

        window.Status = toStatus;
        window.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var campaign = await db.MigrationCampaigns.FirstAsync(c => c.Id == window.MigrationCampaignId);
        await audit.RecordUserActionAsync("", actorName, "ChangeWindowStatusChanged", "MigrationCampaign", window.MigrationCampaignId.ToString(), campaign.Name,
            reason: $"\"{window.Title}\" -> {toStatus}");
        return (true, "");
    }

    public async Task<bool> SetEmergencyExceptionAsync(int windowId, int? exceptionId, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        var window = await db.ChangeWindows.FirstOrDefaultAsync(w => w.Id == windowId);
        if (window is null)
        {
            return false;
        }

        window.EmergencyExceptionId = exceptionId;
        window.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var campaign = await db.MigrationCampaigns.FirstAsync(c => c.Id == window.MigrationCampaignId);
        await audit.RecordUserActionAsync("", actorName, "ChangeWindowEmergencyExceptionLinked", "MigrationCampaign", window.MigrationCampaignId.ToString(), campaign.Name,
            reason: $"\"{window.Title}\" linked to exception #{exceptionId}");
        return true;
    }
}
