using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using DBTracker.Infrastructure.Audit;
using DBTracker.Infrastructure.Identity;
using DBTracker.Infrastructure.Notifications;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace DBTracker.Infrastructure.Lifecycle;

public record InstanceRunwayView(
    DatabaseInstance Instance,
    VersionIdentity? Identity,
    ReleaseCatalogEntry? MatchedCatalogEntry,
    SupportRunwayResult Runway,
    SupportRunwayDecision? LatestDecision);

/// <summary>Finds each instance's catalogue match, persists notice dedupe state, and sends a notice when a more urgent threshold is crossed. The math is in SupportRunwayCalculator.</summary>
public class SupportRunwayService(
    IDbContextFactory<DBTrackerDbContext> factory,
    AuditRecorder audit,
    NotificationService notifications,
    NotificationSubscriptionService subscriptionService,
    IEmailSender emailSender,
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration)
{
    public async Task<InstanceRunwayView?> GetForInstanceAsync(int instanceId)
    {
        using var db = await factory.CreateDbContextAsync();
        var instance = await db.DatabaseInstances.AsNoTracking().FirstOrDefaultAsync(i => i.Id == instanceId);
        if (instance is null)
        {
            return null;
        }

        var identity = await db.VersionIdentities.AsNoTracking().FirstOrDefaultAsync(v => v.DatabaseInstanceId == instanceId);
        var matched = await MatchCatalogEntryAsync(db, instance.Engine, identity);
        var contracts = await ContractsForAsync(db, instanceId);
        var runway = SupportRunwayCalculator.Evaluate(identity, matched, contracts, DateTime.UtcNow);
        var latestDecision = await db.SupportRunwayDecisions.AsNoTracking()
            .Where(d => d.DatabaseInstanceId == instanceId)
            .OrderByDescending(d => d.DecidedAtUtc)
            .FirstOrDefaultAsync();

        return new InstanceRunwayView(instance, identity, matched, runway, latestDecision);
    }

    private static async Task<ReleaseCatalogEntry?> MatchCatalogEntryAsync(DBTrackerDbContext db, DatabaseEngine engine, VersionIdentity? identity)
    {
        if (identity is null || identity.ResolutionStatus != VersionResolutionStatus.Resolved)
        {
            return null;
        }

        var candidates = await db.ReleaseCatalogEntries.AsNoTracking()
            .Where(c => c.Engine == engine && c.Family == identity.Family && c.Major == identity.Major)
            .ToListAsync();
        return ReleaseCatalogMatcher.Match(candidates, engine, identity);
    }

    private static async Task<List<SupportContract>> ContractsForAsync(DBTrackerDbContext db, int instanceId) =>
        await db.SupportContractAssignments.AsNoTracking()
            .Where(a => a.DatabaseInstanceId == instanceId)
            .Select(a => a.SupportContract!)
            .ToListAsync();

    public async Task RecordDecisionAsync(int instanceId, int? releaseCatalogEntryId, SupportActionType actionType, string actionReference, string notes, string decidedBy)
    {
        using var db = await factory.CreateDbContextAsync();
        var decision = new SupportRunwayDecision
        {
            DatabaseInstanceId = instanceId,
            ReleaseCatalogEntryId = releaseCatalogEntryId,
            ActionType = actionType,
            ActionReference = actionReference,
            Notes = notes,
            DecidedBy = decidedBy
        };
        db.SupportRunwayDecisions.Add(decision);
        await db.SaveChangesAsync();

        var instance = await db.DatabaseInstances.FirstAsync(i => i.Id == instanceId);
        await audit.RecordUserActionAsync("", decidedBy, "SupportRunwayDecisionRecorded", "DatabaseInstance", instanceId.ToString(), instance.Name,
            reason: $"{actionType}: {actionReference}");
    }

    /// <summary>One sweep: raise one deduplicated notice per instance that crossed a more urgent threshold. Shared by the hosted service and the manual action so dedupe behaves the same.</summary>
    public async Task<int> EvaluateAndNotifyAsync(CancellationToken ct = default)
    {
        using var db = await factory.CreateDbContextAsync(ct);
        var thresholds = SupportRunwayNoticeEvaluator.ParseThresholds(configuration["SupportRunway:NoticeDaysThresholds"]);
        var now = DateTime.UtcNow;

        var instances = await db.DatabaseInstances.Where(i => !i.IsArchived).ToListAsync(ct);
        var notifiedCount = 0;

        // Instances with no version identity have no row to hold dedupe state. Rather than invent an identity nobody observed,
        // the one Unknown notice each gets is recorded in the audit trail, and that record is the dedupe state.
        var noIdentityNoticeSent = (await db.AuditEvents.AsNoTracking()
                .Where(a => a.Action == SupportRunwayNoticeEvaluator.NoIdentityNoticeAuditAction && a.TargetType == "DatabaseInstance")
                .Select(a => a.TargetId)
                .ToListAsync(ct))
            .ToHashSet();

        foreach (var instance in instances)
        {
            var identity = await db.VersionIdentities.FirstOrDefaultAsync(v => v.DatabaseInstanceId == instance.Id, ct);
            var matched = await MatchCatalogEntryAsync(db, instance.Engine, identity);
            var runway = SupportRunwayCalculator.Evaluate(identity, matched, await ContractsForAsync(db, instance.Id), now);

            var last = SupportRunwayNoticeEvaluator.LastStateFor(identity, noIdentityNoticeSent.Contains(instance.Id.ToString()));
            var decision = SupportRunwayNoticeEvaluator.Decide(runway, thresholds, last);
            if (identity is not null && decision.StateChanged)
            {
                identity.LastRunwayNoticeThresholdDays = decision.NewState.ThresholdDays;
                identity.LastRunwayNoticeMilestoneUtc = decision.NewState.MilestoneUtc;
                identity.LastRunwayNoticeKind = decision.NewState.Kind ?? "";
            }
            if (!decision.Notify)
            {
                continue;
            }

            string message;
            if (runway.IsUnknown)
            {
                message = runway.NeedsReview
                    ? $"{instance.Name}: support runway needs review. {runway.UnknownReason}"
                    : $"{instance.Name}: support runway is unknown. {runway.UnknownReason}";
            }
            else
            {
                var daysDescription = runway.DaysToNextMilestone is { } d && d <= 0
                    ? $"{Math.Abs(d)} day(s) overdue"
                    : $"{runway.DaysToNextMilestone} day(s) remaining";
                message = $"{instance.Name}: {runway.NextMilestoneLabel} {runway.NextMilestoneUtc:yyyy-MM-dd}, {daysDescription} ({runway.Phase}).";
                if (!string.IsNullOrEmpty(runway.CoverageNote))
                {
                    message += " " + runway.CoverageNote;
                }
            }
            await notifications.CreateAsync(instance, message, runway.Phase == SupportPhase.EndOfLife ? NotificationSeverity.Critical : NotificationSeverity.Warning);

            var recipientEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(instance.ContactEmail))
            {
                recipientEmails.Add(instance.ContactEmail);
            }
            foreach (var userId in await subscriptionService.GetEmailSubscriberUserIdsAsync(instance.Id, instance.Company, instance.OwnerTeamId))
            {
                var user = await userManager.FindByIdAsync(userId);
                if (user?.Email is { Length: > 0 } subscriberEmail)
                {
                    recipientEmails.Add(subscriberEmail);
                }
            }
            foreach (var email in recipientEmails)
            {
                await emailSender.SendAsync(email, $"DBTracker support-runway notice: {instance.Name}", message, ct);
            }

            if (identity is not null)
            {
                identity.LastRunwayNoticeAtUtc = now;
            }
            else
            {
                await audit.RecordSystemActionAsync("SupportRunwayCheck", SupportRunwayNoticeEvaluator.NoIdentityNoticeAuditAction, "DatabaseInstance",
                    instance.Id.ToString(), instance.Name, message, ct);
                noIdentityNoticeSent.Add(instance.Id.ToString());
            }
            notifiedCount++;
        }

        await db.SaveChangesAsync(ct);
        if (notifiedCount > 0)
        {
            await audit.RecordSystemActionAsync("SupportRunwayCheck", "SupportRunwayNoticesRaised", "DatabaseInstance", "", $"{notifiedCount} instance(s)", ct: ct);
        }

        return notifiedCount;
    }
}
