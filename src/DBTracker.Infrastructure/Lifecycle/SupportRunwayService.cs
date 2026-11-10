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

/// <summary>Everything SupportRunwayService needs about one instance's runway, in one place for the UI.</summary>
public record InstanceRunwayView(
    DatabaseInstance Instance,
    VersionIdentity? Identity,
    ReleaseCatalogEntry? MatchedCatalogEntry,
    SupportRunwayResult Runway,
    SupportRunwayDecision? LatestDecision);

/// <summary>
/// D03 — support runway/deadline computation and the escalating, deduplicated notice sweep.
/// Matching an instance's resolved identity to a catalogue entry, and the phase/days-remaining math
/// itself, are pure (SupportRunwayCalculator); this class is just the I/O around it: finding the
/// match, persisting dedupe state, and routing a notice through the existing U07 subscription/
/// notification plumbing when a more urgent threshold is crossed.
/// </summary>
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
        var runway = SupportRunwayCalculator.Compute(matched, DateTime.UtcNow);
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

        return await db.ReleaseCatalogEntries.AsNoTracking()
            .Where(c => c.Engine == engine && c.Family == identity.Family && c.Major == identity.Major)
            .OrderByDescending(c => c.Minor ?? int.MinValue)
            .FirstOrDefaultAsync();
    }

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

    /// <summary>
    /// One sweep: for every non-archived instance with a resolved identity matched to a catalogue
    /// entry, check whether a more urgent configured threshold has been crossed since the last
    /// notice, and if so raise exactly one deduplicated in-app + email notice. Safe to call either
    /// from the background hosted service or an explicit "Evaluate now" admin action — both paths
    /// share this one method so the behavior (and dedupe state) is identical either way.
    /// </summary>
    public async Task<int> EvaluateAndNotifyAsync(CancellationToken ct = default)
    {
        using var db = await factory.CreateDbContextAsync(ct);
        var thresholds = SupportRunwayNoticeEvaluator.ParseThresholds(configuration["SupportRunway:NoticeDaysThresholds"]);
        var now = DateTime.UtcNow;

        var instances = await db.DatabaseInstances.Where(i => !i.IsArchived).ToListAsync(ct);
        var notifiedCount = 0;

        foreach (var instance in instances)
        {
            var identity = await db.VersionIdentities.FirstOrDefaultAsync(v => v.DatabaseInstanceId == instance.Id, ct);
            var matched = await MatchCatalogEntryAsync(db, instance.Engine, identity);
            var runway = SupportRunwayCalculator.Compute(matched, now);

            if (identity is null || runway.IsUnknown)
            {
                continue;
            }

            var crossed = SupportRunwayNoticeEvaluator.ThresholdCrossed(runway.DaysToNextMilestone, thresholds);
            if (crossed is null || !SupportRunwayNoticeEvaluator.ShouldNotify(runway.DaysToNextMilestone, thresholds, identity.LastRunwayNoticeThresholdDays))
            {
                continue;
            }

            var daysDescription = runway.DaysToNextMilestone is { } d && d <= 0
                ? $"{Math.Abs(d)} day(s) overdue"
                : $"{runway.DaysToNextMilestone} day(s) remaining";

            var message = $"{instance.Name}: {runway.NextMilestoneLabel} — {daysDescription} ({runway.Phase}).";
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

            identity.LastRunwayNoticeThresholdDays = crossed;
            identity.LastRunwayNoticeAtUtc = now;
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
