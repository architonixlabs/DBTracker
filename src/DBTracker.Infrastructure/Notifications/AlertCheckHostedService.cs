using DBTracker.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DBTracker.Infrastructure.Notifications;

/// <summary>
/// Periodically recomputes every active instance's backup health and raises an alert (in-app
/// notification + email to the instance's contact) the moment it crosses into Overdue or Failed.
/// Interval: "Alerts:IntervalMinutes" (default 10); &lt;= 0 disables it.
/// </summary>
public class AlertCheckHostedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<AlertCheckHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = configuration.GetValue("Alerts:IntervalMinutes", 10);
        if (intervalMinutes <= 0)
        {
            logger.LogInformation("Alert check background service disabled (Alerts:IntervalMinutes <= 0).");
            return;
        }

        var interval = TimeSpan.FromMinutes(intervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAllInstancesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Alert check sweep failed.");
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
            // being remembered as already-notified.
            if (AlertEvaluator.IsSuppressedByMaintenance(instance.MaintenanceUntilUtc, now))
            {
                continue;
            }

            var shouldNotify = AlertEvaluator.ShouldNotify(instance.LastNotifiedHealth, health);

            if (shouldNotify)
            {
                var message = $"{instance.Name} is now {DescribeHealth(health)}.";
                await notifications.CreateAsync(instance, message, health == Core.Enums.BackupHealth.Failed
                    ? Core.Enums.NotificationSeverity.Critical
                    : Core.Enums.NotificationSeverity.Warning);

                if (!string.IsNullOrWhiteSpace(instance.ContactEmail))
                {
                    await emailSender.SendAsync(
                        instance.ContactEmail,
                        $"DBTracker alert: {instance.Name} is {DescribeHealth(health)}",
                        $"{message}\n\nApplication: {instance.Application}\nCompany: {instance.Company}\n\nOpen DBTracker to view backup history and log a fix.",
                        ct);
                }

                await webhookSender.SendAsync(WebhookPayloadBuilder.BuildAlertPayload(instance, health, now), ct);

                instance.LastNotifiedUtc = now;
            }

            instance.LastNotifiedHealth = AlertEvaluator.NextNotifiedState(health);
        }

        await db.SaveChangesAsync(ct);
    }

    private static string DescribeHealth(Core.Enums.BackupHealth health) => health switch
    {
        Core.Enums.BackupHealth.Overdue => "overdue for backup",
        Core.Enums.BackupHealth.Failed => "failing backups",
        _ => health.ToString()
    };
}
