using DBTracker.Core.Services;

namespace DBTracker.Infrastructure.Notifications;

/// <summary>Outbound alert webhook as a plain JSON POST, so any receiver that accepts JSON works.</summary>
public interface IWebhookSender
{
    /// <summary>Sends the payload. Disabled when no URL is configured; Failed carries the status code or error, never the URL or secret.</summary>
    Task<EmailSendResult> SendAsync(WebhookAlertPayload payload, CancellationToken ct = default);
}
