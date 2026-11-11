using DBTracker.Core.Services;
using Microsoft.Extensions.Logging;

namespace DBTracker.Infrastructure.Notifications;

/// <summary>Default when no webhook URL is configured: logs instead of sending.</summary>
public class NullWebhookSender(ILogger<NullWebhookSender> logger) : IWebhookSender
{
    public Task<EmailSendResult> SendAsync(WebhookAlertPayload payload, CancellationToken ct = default)
    {
        logger.LogInformation("Webhook URL not configured. Would have sent: {Text}", payload.Text);
        return Task.FromResult(EmailSendResult.Disabled);
    }
}
