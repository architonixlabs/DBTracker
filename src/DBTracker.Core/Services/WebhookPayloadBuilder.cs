using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

public record WebhookAlertPayload(string Event, string Text, string InstanceName, string Application, string Company, string Department, string Health, DateTime TimestampUtc);

/// <summary>Webhook alert payload. A top-level "text" field works with Slack incoming webhooks and most generic receivers; the caller serializes and sends it.</summary>
public static class WebhookPayloadBuilder
{
    public static WebhookAlertPayload BuildAlertPayload(DatabaseInstance instance, BackupHealth health, DateTime nowUtc)
    {
        var healthText = health switch
        {
            BackupHealth.Failed => "failing backups",
            BackupHealth.Overdue => "overdue for backup",
            _ => health.ToString()
        };

        return new WebhookAlertPayload(
            Event: "backup_health_alert",
            Text: $"DBTracker: {instance.Name} is {healthText} ({instance.Application}, {instance.Company}/{instance.Department}).",
            InstanceName: instance.Name,
            Application: instance.Application,
            Company: instance.Company,
            Department: instance.Department,
            Health: health.ToString(),
            TimestampUtc: nowUtc);
    }
}
