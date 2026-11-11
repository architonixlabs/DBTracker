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

/// <summary>Recomputes every active instance's health and alerts (in-app, plus email to the contact and Email/Both subscribers outside their quiet hours) when it crosses into an alertable state, opening, escalating or closing the instance's incident in step.</summary>
/// <remarks>Alerts:IntervalMinutes (default 10); &lt;= 0 disables it.</remarks>
public class AlertCheckHostedService(
    IServiceScopeFactory scopeFactory,
    Install.InstallState installState,
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
        await installState.WaitUntilInstalledAsync(stoppingToken);
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
        var context = new AlertContext(
            scope.ServiceProvider.GetRequiredService<NotificationService>(),
            scope.ServiceProvider.GetRequiredService<NotificationSubscriptionService>(),
            scope.ServiceProvider.GetRequiredService<NotificationPreferenceService>(),
            scope.ServiceProvider.GetRequiredService<DeliveryAttemptService>(),
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
            scope.ServiceProvider.GetRequiredService<IEmailSender>(),
            scope.ServiceProvider.GetRequiredService<IWebhookSender>(),
            logger);

        var graceHours = configuration.GetValue("Alerts:NeverBackedUpGraceHours", 24);
        var escalationMinutes = configuration.GetValue("Alerts:EscalationIntervalMinutes", 240);
        var emailOnRecovery = configuration.GetValue("Alerts:EmailOnRecovery", true);

        var instances = await db.DatabaseInstances
            .Where(i => !i.IsArchived)
            .Include(i => i.BackupRecords)
            .Include(i => i.RestoreEvents)
            .AsSplitQuery()
            .ToListAsync(ct);

        var now = DateTime.UtcNow;

        foreach (var instance in instances)
        {
            var records = instance.BackupRecords.ToList();
            var health = BackupHealthCalculator.Compute(instance, records, now);
            var lastSuccessUtc = BackupHealthCalculator.LastSuccessfulBackupUtc(records, now);
            var evidence = BackupEvidenceClassifier.ClassifyLatestSuccess(records, instance.RestoreEvents.ToList(), now);

            // Maintenance suppresses the alert and the state update, so a still-bad health re-alerts when the window ends.
            // Any open incident stays open; it just doesn't page anyone until then.
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
                    await RaiseAlertAsync(context, instance, health, lastSuccessUtc, evidence, now, ct);
                }
                else if (IncidentEscalationEvaluator.ShouldEscalate(incident, now, escalationMinutes))
                {
                    incident.EscalationLevel++;
                    incident.LastEscalatedAtUtc = now;
                    await RaiseAlertAsync(context, instance, health, lastSuccessUtc, evidence, now, ct, escalationLevel: incident.EscalationLevel);
                }
            }
            else if (health == Core.Enums.BackupHealth.OnTrack)
            {
                var hadOpenIncident = await IncidentLifecycle.CloseOpenAsync(db, instance.Id, now);
                if (hadOpenIncident)
                {
                    var message = AlertMessageBuilder.BuildRecovery(instance.Name, lastSuccessUtc, evidence);
                    await context.Notifications.CreateAsync(instance, message, Core.Enums.NotificationSeverity.Info);

                    // Recovery reaches the same people the alert did, so nobody keeps working an incident that has closed.
                    if (emailOnRecovery)
                    {
                        await EmailRecipientsAsync(context, instance, Core.Enums.DeliveryKind.Recovery,
                            $"DBTracker: {instance.Name} has recovered",
                            $"{message}\n\nApplication: {instance.Application}\nCompany: {instance.Company}\n\nThe incident is closed. Open DBTracker for the backup history.",
                            isCritical: false, now, ct);
                    }
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

    private sealed record AlertContext(
        NotificationService Notifications,
        NotificationSubscriptionService Subscriptions,
        NotificationPreferenceService Preferences,
        DeliveryAttemptService DeliveryAttempts,
        UserManager<ApplicationUser> UserManager,
        IEmailSender EmailSender,
        IWebhookSender WebhookSender,
        ILogger Logger);

    private static async Task RaiseAlertAsync(
        AlertContext context,
        Core.Entities.DatabaseInstance instance,
        Core.Enums.BackupHealth health,
        DateTime? lastSuccessUtc,
        Core.Enums.EvidenceLevel? evidence,
        DateTime now,
        CancellationToken ct,
        int? escalationLevel = null)
    {
        var message = AlertMessageBuilder.BuildAlert(instance.Name, health, lastSuccessUtc, evidence, escalationLevel);
        var isCritical = health == Core.Enums.BackupHealth.Failed;

        await context.Notifications.CreateAsync(instance, message, isCritical
            ? Core.Enums.NotificationSeverity.Critical
            : Core.Enums.NotificationSeverity.Warning);

        await EmailRecipientsAsync(context, instance, Core.Enums.DeliveryKind.Alert,
            $"DBTracker alert: {instance.Name} is {AlertMessageBuilder.DescribeHealth(health)}",
            $"{message}\n\nApplication: {instance.Application}\nCompany: {instance.Company}\n\nOpen DBTracker to view backup history and log a fix.",
            isCritical, now, ct);

        await context.WebhookSender.SendAsync(WebhookPayloadBuilder.BuildAlertPayload(instance, health, now), ct);
    }

    /// <summary>Emails the instance contact and Email/Both subscribers, skipping subscribers in their quiet hours, and stores the attempt.</summary>
    /// <remarks>Quiet hours belong to subscribers (accounts with preferences), not to the instance contact address, which has none.</remarks>
    private static async Task EmailRecipientsAsync(
        AlertContext context,
        Core.Entities.DatabaseInstance instance,
        Core.Enums.DeliveryKind kind,
        string subject,
        string body,
        bool isCritical,
        DateTime now,
        CancellationToken ct)
    {
        var recipientEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(instance.ContactEmail))
        {
            recipientEmails.Add(instance.ContactEmail);
        }

        var subscriberUserIds = await context.Subscriptions.GetEmailSubscriberUserIdsAsync(instance.Id, instance.Company, instance.OwnerTeamId);
        var holding = await context.Preferences.GetUsersHoldingEmailAsync(subscriberUserIds, isCritical, now);
        var held = 0;
        foreach (var userId in subscriberUserIds)
        {
            var user = await context.UserManager.FindByIdAsync(userId);
            if (user?.Email is not { Length: > 0 } email || recipientEmails.Contains(email))
            {
                continue;
            }

            if (holding.Contains(userId))
            {
                held++;
                continue;
            }

            recipientEmails.Add(email);
        }

        var results = new List<EmailSendResult>(recipientEmails.Count);
        foreach (var email in recipientEmails)
        {
            var emailResult = await context.EmailSender.SendAsync(email, subject, body, ct);
            results.Add(emailResult);

            if (emailResult.Outcome == Core.Enums.EmailSendOutcome.Failed)
            {
                context.Logger.LogWarning("{Kind} email to {Email} for {Instance} failed: {Detail}", kind, email, instance.Name, emailResult.Detail);
            }
        }

        try
        {
            await context.DeliveryAttempts.RecordAsync(kind, instance.Name, results,
                AuditEventFactory.SystemActorPrefix + WorkerName, held, now, ct);
        }
        catch (Exception ex)
        {
            // The history is diagnostic: failing to store it must not stop the sweep saving incident state.
            context.Logger.LogWarning(ex, "Could not store the delivery attempt for {Instance}.", instance.Name);
        }
    }
}
