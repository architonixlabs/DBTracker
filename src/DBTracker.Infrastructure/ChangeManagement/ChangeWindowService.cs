using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using DBTracker.Infrastructure.Audit;
using DBTracker.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.ChangeManagement;

/// <summary>Change windows, runbook steps, change freezes and the cutover journal. Every write is a recorded fact that executes and sends
/// nothing; conflict detection and the approval gate are pure and fed from here.</summary>
public class ChangeWindowService(IDbContextFactory<DBTrackerDbContext> factory, AuditRecorder audit)
{
    private static readonly string[] EditorRoles = [Roles.Admin, Roles.Dba];
    private static readonly string[] FreezeRoles = [Roles.Admin, Roles.ItManager];

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

    /// <summary>Read-only conflict check against other scheduled windows on the same instances, change freezes for those instances'
    /// environments, and existing maintenance windows. Never blocks saving a plan; freezes block approval in SetStatusAsync.</summary>
    public async Task<ChangeWindowConflictResult> DetectConflictsAsync(int campaignId, DateTime startUtc, DateTime endUtc, int? excludingWindowId = null)
    {
        using var db = await factory.CreateDbContextAsync();
        return await DetectConflictsAsync(db, campaignId, startUtc, endUtc, excludingWindowId);
    }

    private static async Task<ChangeWindowConflictResult> DetectConflictsAsync(DBTrackerDbContext db, int campaignId, DateTime startUtc, DateTime endUtc, int? excludingWindowId)
    {
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

        var instances = await db.DatabaseInstances
            .Where(i => instanceIds.Contains(i.Id))
            .Select(i => new { i.Name, i.MaintenanceUntilUtc, i.MaintenanceReason, i.Environment })
            .ToListAsync();
        var maintenance = instances
            .Where(i => i.MaintenanceUntilUtc != null)
            .Select(i => new InstanceMaintenanceWindow(i.Name, i.MaintenanceUntilUtc, i.MaintenanceReason))
            .ToList();
        // A campaign with no linked instances can't say which environments it touches, so every environment's freeze applies.
        var environments = instances.Count == 0 ? Enum.GetValues<EnvironmentTier>().ToList() : instances.Select(i => i.Environment).Distinct().ToList();

        var freezes = await db.ChangeFreezePeriods
            .Where(f => f.CancelledAtUtc == null && f.StartUtc < endUtc && startUtc < f.EndUtc)
            .Select(f => new ChangeFreezeWindow(f.Id, f.Name, f.StartUtc, f.EndUtc, f.AppliesToEnvironment, f.Reason))
            .ToListAsync();

        return ChangeWindowConflictDetector.Detect(startUtc, endUtc, otherWindows, maintenance, freezes, environments);
    }

    /// <summary>Plans a window. At least one required approval is mandatory, since a window with none would pass the approval gate unchecked.</summary>
    public async Task<ChangeWindow> CreateAsync(ChangeWindow window, IEnumerable<string> requiredApprovalScopes, string actorName)
    {
        var scopes = requiredApprovalScopes.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        if (scopes.Count == 0)
        {
            throw new ArgumentException("A change window needs at least one required approval.", nameof(requiredApprovalScopes));
        }

        using var db = await factory.CreateDbContextAsync();
        window.CreatedAtUtc = DateTime.UtcNow;
        window.UpdatedAtUtc = DateTime.UtcNow;
        window.CreatedBy = actorName;
        window.Status = ChangeWindowStatus.Planned;

        foreach (var scope in scopes)
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

    public async Task<(bool Ok, string Reason)> AddRunbookStepAsync(int windowId, string description, bool isRequired, string actorName, IReadOnlyCollection<string> actorRoles)
    {
        if (!actorRoles.Any(EditorRoles.Contains))
        {
            return (false, "Only Admin and DBA users can edit runbook steps.");
        }
        if (string.IsNullOrWhiteSpace(description))
        {
            return (false, "Describe the step.");
        }
        if (description.Trim().Length > 500)
        {
            return (false, "Keep the step under 500 characters.");
        }

        using var db = await factory.CreateDbContextAsync();
        var window = await db.ChangeWindows.Include(w => w.RunbookSteps).FirstOrDefaultAsync(w => w.Id == windowId);
        if (window is null)
        {
            return (false, "Change window not found.");
        }
        if (IsFinal(window.Status))
        {
            return (false, $"The window is {window.Status}, so its runbook can no longer change.");
        }

        var order = window.RunbookSteps.Count == 0 ? 1 : window.RunbookSteps.Max(s => s.Order) + 1;
        db.ChangeWindowRunbookSteps.Add(new ChangeWindowRunbookStep { ChangeWindowId = windowId, Order = order, Description = description.Trim(), IsRequired = isRequired });
        await db.SaveChangesAsync();

        var campaign = await db.MigrationCampaigns.FirstAsync(c => c.Id == window.MigrationCampaignId);
        await audit.RecordUserActionAsync("", actorName, "ChangeWindowRunbookStepAdded", "MigrationCampaign", window.MigrationCampaignId.ToString(), campaign.Name,
            reason: $"\"{window.Title}\" step {order}{(isRequired ? " (required)" : "")}: {description.Trim()}");
        return (true, "");
    }

    /// <summary>Marks a runbook step done or not done. Self-reported: it records what the person says was done elsewhere and runs nothing.</summary>
    public async Task<(bool Ok, string Reason)> SetRunbookStepCompletedAsync(int stepId, bool completed, string actorName, IReadOnlyCollection<string> actorRoles)
    {
        if (!actorRoles.Any(EditorRoles.Contains))
        {
            return (false, "Only Admin and DBA users can update runbook steps.");
        }

        using var db = await factory.CreateDbContextAsync();
        var step = await db.ChangeWindowRunbookSteps.Include(s => s.ChangeWindow).FirstOrDefaultAsync(s => s.Id == stepId);
        if (step?.ChangeWindow is null)
        {
            return (false, "Runbook step not found.");
        }
        if (IsFinal(step.ChangeWindow.Status))
        {
            return (false, $"The window is {step.ChangeWindow.Status}, so its runbook can no longer change.");
        }

        step.IsCompleted = completed;
        await db.SaveChangesAsync();

        var campaign = await db.MigrationCampaigns.FirstAsync(c => c.Id == step.ChangeWindow.MigrationCampaignId);
        await audit.RecordUserActionAsync("", actorName, completed ? "ChangeWindowRunbookStepCompleted" : "ChangeWindowRunbookStepReopened",
            "MigrationCampaign", step.ChangeWindow.MigrationCampaignId.ToString(), campaign.Name,
            reason: $"\"{step.ChangeWindow.Title}\" step {step.Order}: {step.Description}");
        return (true, "");
    }

    /// <summary>Appends a journal entry. TimestampUtc is when it happened, entered by the person; it may be earlier than now but never later.</summary>
    public async Task<(bool Ok, string Reason)> AddJournalEntryAsync(int windowId, CutoverJournalEntry entry, string actorName, IReadOnlyCollection<string> actorRoles)
    {
        if (!actorRoles.Any(EditorRoles.Contains))
        {
            return (false, "Only Admin and DBA users can add journal entries.");
        }

        var now = DateTime.UtcNow;
        var error = ChangeEvidenceRules.ValidateJournalEntry(entry.Description, entry.TimestampUtc, now);
        if (error is not null)
        {
            return (false, error);
        }

        using var db = await factory.CreateDbContextAsync();
        var window = await db.ChangeWindows.FirstOrDefaultAsync(w => w.Id == windowId);
        if (window is null)
        {
            return (false, "Change window not found.");
        }

        entry.ChangeWindowId = windowId;
        entry.TimestampUtc = DateTime.SpecifyKind(entry.TimestampUtc, DateTimeKind.Utc);
        entry.RecordedBy = actorName;
        entry.RecordedAtUtc = now;
        db.CutoverJournalEntries.Add(entry);
        await db.SaveChangesAsync();

        var campaign = await db.MigrationCampaigns.FirstAsync(c => c.Id == window.MigrationCampaignId);
        await audit.RecordUserActionAsync("", actorName, "CutoverJournalEntryRecorded", "MigrationCampaign", window.MigrationCampaignId.ToString(), campaign.Name,
            reason: $"\"{window.Title}\" ({entry.EntryType}, happened {entry.TimestampUtc:u}): {entry.Description}");
        return (true, "");
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

    /// <summary>The only path that changes ChangeWindow.Status. Refuses moves outside ChangeWindowStatusTransitionValidator, and Approved/InProgress
    /// without the approval gate or during a change freeze, unless an active emergency exception is linked.</summary>
    public async Task<(bool Ok, string Reason)> SetStatusAsync(int windowId, ChangeWindowStatus toStatus, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        var window = await db.ChangeWindows.Include(w => w.Approvals).FirstOrDefaultAsync(w => w.Id == windowId);
        if (window is null)
        {
            return (false, "Change window not found.");
        }

        if (!ChangeWindowStatusTransitionValidator.IsValidTransition(window.Status, toStatus))
        {
            return (false, ChangeWindowStatusTransitionValidator.DescribeInvalidReason(window.Status, toStatus));
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

            var conflicts = await DetectConflictsAsync(db, window.MigrationCampaignId, window.PlannedStartUtc, window.PlannedEndUtc, window.Id);
            if (conflicts.BlocksApproval(hasActiveException))
            {
                return (false, string.Join(" ", conflicts.FreezeConflicts) + " Move the window, or link an approved emergency exception for it.");
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

    /// <summary>Links an emergency exception recorded for this window (TargetType ChangeWindow, TargetId the window), or clears the link with null.
    /// An exception for some other record can't be borrowed.</summary>
    public async Task<(bool Ok, string Reason)> SetEmergencyExceptionAsync(int windowId, int? exceptionId, string actorName)
    {
        using var db = await factory.CreateDbContextAsync();
        var window = await db.ChangeWindows.FirstOrDefaultAsync(w => w.Id == windowId);
        if (window is null)
        {
            return (false, "Change window not found.");
        }

        if (exceptionId is { } id)
        {
            var exception = await db.ReadinessExceptions.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
            if (exception is null)
            {
                return (false, $"Exception #{id} doesn't exist.");
            }
            if (exception.TargetType != ReadinessExceptionTargets.ChangeWindow || exception.TargetId != windowId)
            {
                return (false, $"Exception #{id} was recorded for {ReadinessExceptionTargets.Describe(exception.TargetType)} #{exception.TargetId}, not this change window.");
            }
        }

        window.EmergencyExceptionId = exceptionId;
        window.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var campaign = await db.MigrationCampaigns.FirstAsync(c => c.Id == window.MigrationCampaignId);
        await audit.RecordUserActionAsync("", actorName, "ChangeWindowEmergencyExceptionLinked", "MigrationCampaign", window.MigrationCampaignId.ToString(), campaign.Name,
            reason: exceptionId is null ? $"\"{window.Title}\" exception link cleared" : $"\"{window.Title}\" linked to exception #{exceptionId}");
        return (true, "");
    }

    public async Task<List<ChangeFreezePeriod>> ListFreezesAsync(bool includePast = false)
    {
        using var db = await factory.CreateDbContextAsync();
        var now = DateTime.UtcNow;
        var query = db.ChangeFreezePeriods.AsNoTracking();
        if (!includePast)
        {
            query = query.Where(f => f.EndUtc > now);
        }
        return await query.OrderBy(f => f.StartUtc).ToListAsync();
    }

    public async Task<(bool Ok, string Reason)> CreateFreezeAsync(ChangeFreezePeriod freeze, string actorName, IReadOnlyCollection<string> actorRoles)
    {
        if (!actorRoles.Any(FreezeRoles.Contains))
        {
            return (false, "Only Admin and IT manager users can declare change freezes.");
        }
        if (string.IsNullOrWhiteSpace(freeze.Name))
        {
            return (false, "Name the freeze.");
        }
        if (freeze.EndUtc <= freeze.StartUtc)
        {
            return (false, "The freeze must end after it starts.");
        }
        if (freeze.EndUtc <= DateTime.UtcNow)
        {
            return (false, "The freeze has already ended.");
        }

        using var db = await factory.CreateDbContextAsync();
        freeze.Name = freeze.Name.Trim();
        freeze.StartUtc = DateTime.SpecifyKind(freeze.StartUtc, DateTimeKind.Utc);
        freeze.EndUtc = DateTime.SpecifyKind(freeze.EndUtc, DateTimeKind.Utc);
        freeze.CreatedBy = actorName;
        freeze.CreatedAtUtc = DateTime.UtcNow;
        db.ChangeFreezePeriods.Add(freeze);
        await db.SaveChangesAsync();

        await audit.RecordUserActionAsync("", actorName, "ChangeFreezeDeclared", "ChangeFreezePeriod", freeze.Id.ToString(), freeze.Name,
            reason: $"{freeze.StartUtc:u} to {freeze.EndUtc:u}, {(freeze.AppliesToEnvironment?.ToString() ?? "all environments")}. {freeze.Reason}");
        return (true, "");
    }

    /// <summary>Lifts a freeze early. The row is kept, so the history of what was frozen survives.</summary>
    public async Task<(bool Ok, string Reason)> CancelFreezeAsync(int freezeId, string actorName, IReadOnlyCollection<string> actorRoles)
    {
        if (!actorRoles.Any(FreezeRoles.Contains))
        {
            return (false, "Only Admin and IT manager users can lift change freezes.");
        }

        using var db = await factory.CreateDbContextAsync();
        var freeze = await db.ChangeFreezePeriods.FirstOrDefaultAsync(f => f.Id == freezeId);
        if (freeze is null || freeze.CancelledAtUtc is not null)
        {
            return (false, "That freeze isn't active.");
        }

        freeze.CancelledAtUtc = DateTime.UtcNow;
        freeze.CancelledBy = actorName;
        await db.SaveChangesAsync();

        await audit.RecordUserActionAsync("", actorName, "ChangeFreezeLifted", "ChangeFreezePeriod", freeze.Id.ToString(), freeze.Name);
        return (true, "");
    }

    private static bool IsFinal(ChangeWindowStatus status) =>
        status is ChangeWindowStatus.Completed or ChangeWindowStatus.RolledBack or ChangeWindowStatus.Cancelled;
}
