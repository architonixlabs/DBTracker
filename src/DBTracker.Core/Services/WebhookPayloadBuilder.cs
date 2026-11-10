using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

public record WebhookAlertPayload(string Event, string Text, string InstanceName, string Application, string Company, string Department, string Health, DateTime TimestampUtc);

/// <summary>
/// Builds the payload for an outbound webhook alert. Generic on purpose — a top-level "text" field
/// is understood by Slack incoming webhooks and most other generic receivers, so this stays usable
/// without assuming one specific platform. Pure (no I/O, no serialization — the caller serializes
/// and sends it).
/// </summary>
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
