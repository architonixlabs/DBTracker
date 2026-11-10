using DBTracker.Core.Services;
using DBTracker.Infrastructure.Diagnostics;
using DBTracker.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DBTracker.Infrastructure.Notifications;

/// <summary>
/// Periodically recomputes every active instance's backup health and raises an alert (in-app
/// notification + email to the instance's contact and any matching Email/Both subscribers, U07)
/// the moment it crosses into an alertable health. Also opens/escalates/closes the instance's
/// Incident record (U08) in step with the same alert lifecycle.
/// Interval: "Alerts:IntervalMinutes" (default 10); &lt;= 0 disables it.
/// </summary>
public class AlertCheckHostedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    WorkerHeartbeatStore heartbeats,
    ILogger<AlertCheckHostedService> logger) : BackgroundService
{
    public const string WorkerName = "Alert check";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = configuration.GetValue("Alerts:IntervalMinutes", 10);
        if (intervalMinutes <= 0)
        {
            logger.LogInformation("Alert check background service disabled (Alerts:IntervalMinutes <= 0).");
            heartbeats.Disabled(WorkerName);
            return;
        }

        var interval = TimeSpan.FromMinutes(intervalMinutes);
        heartbeats.Starting(WorkerName, interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAllInstancesAsync(stoppingToken);
                heartbeats.RanSuccessfully(WorkerName, interval);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Alert check sweep failed.");
                heartbeats.RanWithError(WorkerName, interval, ex.Message);
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // shutting down
            }
        }
    }

    private async Task CheckAllInstancesAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DBTrackerDbContext>();
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var webhookSender = scope.ServiceProvider.GetRequiredService<IWebhookSender>();
        var notifications = scope.ServiceProvider.GetRequiredService<NotificationService>();
        var subscriptionService = scope.ServiceProvider.GetRequiredService<NotificationSubscriptionService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var graceHours = configuration.GetValue("Alerts:NeverBackedUpGraceHours", 24);
        var escalationMinutes = configuration.GetValue("Alerts:EscalationIntervalMinutes", 240);

        var instances = await db.DatabaseInstances
            .Where(i => !i.IsArchived)
            .Include(i => i.BackupRecords)
            .AsSplitQuery()
            .ToListAsync(ct);

        var now = DateTime.UtcNow;

        foreach (var instance in instances)
        {
            var health = BackupHealthCalculator.Compute(instance, instance.BackupRecords.ToList(), now);

            // A planned maintenance window suppresses the alert entirely — including the state
            // update — so a still-bad health correctly re-alerts once the window ends instead of
            // being remembered as already-notified. The open incident (if any) is left untouched:
            // it stays open and visible, it just doesn't re-page anyone until maintenance ends.
            if (AlertEvaluator.IsSuppressedByMaintenance(instance.MaintenanceUntilUtc, now))
            {
                continue;
            }

            var suppressNeverBackedUp = AlertEvaluator.IsWithinOnboardingGrace(instance.CreatedAtUtc, now, graceHours);
            var shouldNotify = AlertEvaluator.ShouldNotify(instance.LastNotifiedHealth, health, suppressNeverBackedUp);
            var isAlertableNow = health is Core.Enums.BackupHealth.Overdue or Core.Enums.BackupHealth.Failed ||
                (health is Core.Enums.BackupHealth.NeverBackedUp && !suppressNeverBackedUp);

            if (isAlertableNow)
            {
                var incident = await IncidentLifecycle.OpenOrContinueAsync(db, instance.Id, health, now);

                if (shouldNotify)
                {
                    await RaiseAlertAsync(db, instance, health, now, notifications, subscriptionService, userManager, emailSender, webhookSender, logger, ct);
                }
                else if (IncidentEscalationEvaluator.ShouldEscalate(incident, now, escalationMinutes))
                {
                    incident.EscalationLevel++;
                    incident.LastEscalatedAtUtc = now;
                    await RaiseAlertAsync(db, instance, health, now, notifications, subscriptionService, userManager, emailSender, webhookSender, logger, ct,
                        escalationLevel: incident.EscalationLevel);
                }
            }
            else if (health == Core.Enums.BackupHealth.OnTrack)
            {
                var hadOpenIncident = await IncidentLifecycle.CloseOpenAsync(db, instance.Id, now);
                if (hadOpenIncident)
                {
                    await notifications.CreateAsync(instance, $"{instance.Name} has recovered — backup health is on track again.", Core.Enums.NotificationSeverity.Info);
                }
            }

            instance.LastNotifiedHealth = AlertEvaluator.NextNotifiedState(health);
            if (shouldNotify)
            {
                instance.LastNotifiedUtc = now;
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task RaiseAlertAsync(
        DBTrackerDbContext db,
        Core.Entities.DatabaseInstance instance,
        Core.Enums.BackupHealth health,
        DateTime now,
        NotificationService notifications,
        NotificationSubscriptionService subscriptionService,
        UserManager<ApplicationUser> userManager,
        IEmailSender emailSender,
        IWebhookSender webhookSender,
        ILogger<AlertCheckHostedService> logger,
        CancellationToken ct,
        int? escalationLevel = null)
    {
        var message = escalationLevel is { } level
            ? $"{instance.Name} is still {DescribeHealth(health)} (escalation #{level}, unacknowledged)."
            : $"{instance.Name} is now {DescribeHealth(health)}.";

        await notifications.CreateAsync(instance, message, health == Core.Enums.BackupHealth.Failed
            ? Core.Enums.NotificationSeverity.Critical
            : Core.Enums.NotificationSeverity.Warning);

        var recipientEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(instance.ContactEmail))
        {
            recipientEmails.Add(instance.ContactEmail);
        }

        var subscriberUserIds = await subscriptionService.GetEmailSubscriberUserIdsAsync(instance.Id, instance.Company, instance.OwnerTeamId);
        foreach (var userId in subscriberUserIds)
        {
            var user = await userManager.FindByIdAsync(userId);
            if (user?.Email is { Length: > 0 } email)
            {
                recipientEmails.Add(email);
            }
        }

        foreach (var email in recipientEmails)
        {
            var emailResult = await emailSender.SendAsync(
                email,
                $"DBTracker alert: {instance.Name} is {DescribeHealth(health)}",
                $"{message}\n\nApplication: {instance.Application}\nCompany: {instance.Company}\n\nOpen DBTracker to view backup history and log a fix.",
                ct);

            if (emailResult.Outcome == Core.Enums.EmailSendOutcome.Failed)
            {
                logger.LogWarning("Alert email to {Email} for {Instance} failed: {Detail}", email, instance.Name, emailResult.Detail);
            }
        }

        await webhookSender.SendAsync(WebhookPayloadBuilder.BuildAlertPayload(instance, health, now), ct);
    }

    private static string DescribeHealth(Core.Enums.BackupHealth health) => health switch
    {
        Core.Enums.BackupHealth.Overdue => "overdue for backup",
        Core.Enums.BackupHealth.Failed => "failing backups",
        Core.Enums.BackupHealth.NeverBackedUp => "never backed up",
        _ => health.ToString()
    };
}
