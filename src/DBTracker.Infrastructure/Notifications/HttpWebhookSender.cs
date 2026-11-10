using System.Net.Http.Json;
using DBTracker.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DBTracker.Infrastructure.Notifications;

/// <summary>POSTs the alert payload as JSON to the configured webhook URL — generic enough for Slack incoming webhooks (which key off the top-level "text" field) or any other JSON receiver.</summary>
public class HttpWebhookSender(HttpClient http, IOptions<WebhookOptions> options, ILogger<HttpWebhookSender> logger) : IWebhookSender
{
    public async Task SendAsync(WebhookAlertPayload payload, CancellationToken ct = default)
    {
        var o = options.Value;
        if (string.IsNullOrWhiteSpace(o.Url))
        {
            return;
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
            var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Webhook endpoint returned {Status} for {Instance}.", response.StatusCode, payload.InstanceName);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Webhook delivery failed for {Instance}.", payload.InstanceName);
        }
    }
}
