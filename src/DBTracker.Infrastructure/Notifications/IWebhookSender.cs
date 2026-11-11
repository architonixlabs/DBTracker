using DBTracker.Core.Services;

namespace DBTracker.Infrastructure.Notifications;

/// <summary>Outbound alert webhook as a plain JSON POST, so any receiver that accepts JSON works.</summary>
public interface IWebhookSender
{
    Task SendAsync(WebhookAlertPayload payload, CancellationToken ct = default);
}
