using System.Net.Http.Json;
using DBTracker.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DBTracker.Infrastructure.Notifications;

/// <summary>POSTs the alert payload as JSON to the configured webhook URL.</summary>
public class HttpWebhookSender(HttpClient http, IOptions<WebhookOptions> options, ILogger<HttpWebhookSender> logger) : IWebhookSender
{
    public async Task<EmailSendResult> SendAsync(WebhookAlertPayload payload, CancellationToken ct = default)
    {
        var o = options.Value;
        if (string.IsNullOrWhiteSpace(o.Url))
        {
            return EmailSendResult.Disabled;
        }

        var request = new HttpRequestMessage(HttpMethod.Post, o.Url)
        {
            Content = JsonContent.Create(payload)
        };

        if (!string.IsNullOrWhiteSpace(o.Secret))
        {
            request.Headers.Add("X-DBTracker-Signature", o.Secret);
        }

        try
        {
            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Webhook endpoint returned {Status} for {Instance}.", response.StatusCode, payload.InstanceName);
                return EmailSendResult.Failed($"The webhook endpoint returned HTTP {(int)response.StatusCode} ({response.StatusCode}).");
            }

            return EmailSendResult.Accepted;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Webhook delivery failed for {Instance}.", payload.InstanceName);
            // The URL can carry a token (chat webhooks put it in the path), so it is never copied into the stored error.
            var detail = ex is TaskCanceledException ? "The webhook endpoint did not answer in time." : ex.Message.Replace(o.Url, "[webhook URL]");
            return EmailSendResult.Failed($"Webhook send failed: {detail}");
        }
    }
}
