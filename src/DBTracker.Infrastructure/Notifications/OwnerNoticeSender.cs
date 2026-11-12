using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using DBTracker.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.Notifications;

/// <summary>A notice meant for the owner of one record (an exception, a certificate), not for everyone watching the instance.</summary>
/// <param name="Instance">The instance the record belongs to, when there is one: used for the in-app notification and as the email fallback.</param>
/// <param name="TargetLabel">Shown as the notification's subject line when there is no instance.</param>
public record OwnerNotice(
    string Subject,
    string Message,
    NotificationSeverity Severity,
    string? OwnerEmail,
    int? OwnerTeamId,
    DatabaseInstance? Instance,
    string TargetLabel);

/// <summary>Raises one in-app notification tagged with the owner team, so team subscribers see it, and emails the owner, the owner team's
/// contacts and its email subscribers. The instance contact is emailed only when no owner address exists, and the message says who it went to.</summary>
public class OwnerNoticeSender(IDbContextFactory<DBTrackerDbContext> factory, IEmailSender emailSender, UserManager<ApplicationUser> userManager)
{
    public async Task<OwnerNoticeRecipients> SendAsync(OwnerNotice notice, CancellationToken ct = default)
    {
        using var db = await factory.CreateDbContextAsync(ct);

        Team? team = null;
        var subscriberEmails = new List<string?>();
        if (notice.OwnerTeamId is { } teamId)
        {
            team = await db.Teams.AsNoTracking().FirstOrDefaultAsync(t => t.Id == teamId, ct);
            var subscriberIds = (await db.NotificationSubscriptions.AsNoTracking()
                    .Where(s => s.Scope == SubscriptionScope.Team && s.TeamId == teamId)
                    .ToListAsync(ct))
                .Where(NotificationSubscriptionMatcher.WantsEmail)
                .Select(s => s.UserId)
                .Distinct();
            foreach (var userId in subscriberIds)
            {
                var user = await userManager.FindByIdAsync(userId);
                if (user is { IsActive: true })
                {
                    subscriberEmails.Add(user.Email);
                }
            }
        }

        var recipients = OwnerNoticeRecipientResolver.Resolve(
            notice.OwnerEmail,
            team?.Name,
            [team?.PrimaryContactEmail, team?.BackupContactEmail],
            subscriberEmails,
            notice.Instance?.ContactEmail,
            "the instance contact");

        var message = $"{notice.Message} Sent to {recipients.RoutedTo}.";
        var label = notice.Instance?.Name ?? notice.TargetLabel;
        db.Notifications.Add(new Notification
        {
            DatabaseInstanceId = notice.Instance?.Id,
            InstanceName = label.Length > 200 ? label[..200] : label,
            Company = notice.Instance?.Company ?? "",
            OwnerTeamId = notice.OwnerTeamId ?? notice.Instance?.OwnerTeamId,
            Message = message.Length > 500 ? message[..497] + "..." : message,
            Severity = notice.Severity,
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);

        // People who chose email alerts for this instance still get them; they are watchers, not the owner, so they never replace the owner routing.
        var emails = new HashSet<string>(recipients.Emails, StringComparer.OrdinalIgnoreCase);
        if (notice.Instance is { } instance)
        {
            var watchers = (await db.NotificationSubscriptions.AsNoTracking().ToListAsync(ct))
                .Where(s => NotificationSubscriptionMatcher.WantsEmail(s) &&
                            NotificationSubscriptionMatcher.Matches(s, instance.Id, instance.Company, notice.OwnerTeamId ?? instance.OwnerTeamId))
                .Select(s => s.UserId)
                .Distinct();
            foreach (var userId in watchers)
            {
                var user = await userManager.FindByIdAsync(userId);
                if (user is { IsActive: true, Email.Length: > 0 })
                {
                    emails.Add(user.Email);
                }
            }
        }

        foreach (var email in emails)
        {
            await emailSender.SendAsync(email, notice.Subject, message, ct);
        }

        return recipients;
    }
}
