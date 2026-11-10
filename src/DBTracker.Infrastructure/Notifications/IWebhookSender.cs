using DBTracker.Core.Services;

namespace DBTracker.Infrastructure.Notifications;

/// <summary>
/// Outbound webhook delivery for alerts — a generic HTTP POST rather than a specific platform
/// integration (Slack, Teams, PagerDuty, …), so any receiver that accepts JSON can be wired up.
/// </summary>
public interface IWebhookSender
{
    Task SendAsync(WebhookAlertPayload payload, CancellationToken ct = default);
}
