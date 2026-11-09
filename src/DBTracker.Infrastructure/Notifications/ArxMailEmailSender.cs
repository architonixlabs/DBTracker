using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DBTracker.Infrastructure.Notifications;

/// <summary>Sends via Architonix Labs' own ArxMail Gateway (POST /v1/submit) instead of raw SMTP.</summary>
public class ArxMailEmailSender(HttpClient http, IOptions<ArxMailOptions> options, ILogger<ArxMailEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(string toAddress, string subject, string body, CancellationToken ct = default)
    {
        var o = options.Value;
        if (string.IsNullOrWhiteSpace(o.FromAddress))
        {
            logger.LogWarning("ArxMail email provider selected but FromAddress not configured — skipping send to {To}.", toAddress);
            return;
        }

        var request = new HttpRequestMessage(HttpMethod.Post, o.Endpoint)
        {
            Content = JsonContent.Create(new
            {
                to = toAddress,
                from = o.FromAddress,
                subject,
                body
            })
        };

        if (!string.IsNullOrWhiteSpace(o.ApiKey))
        {
            request.Headers.Add("Authorization", $"Bearer {o.ApiKey}");
        }

        var response = await http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("ArxMail gateway returned {Status} sending to {To}.", response.StatusCode, toAddress);
        }
    }
}
