using Microsoft.Extensions.Logging;

namespace DBTracker.Infrastructure.Notifications;

/// <summary>Default sender when no email provider is configured — logs instead of sending, never throws.</summary>
public class NullEmailSender(ILogger<NullEmailSender> logger) : IEmailSender
{
    public Task<EmailSendResult> SendAsync(string toAddress, string subject, string body, CancellationToken ct = default)
    {
        logger.LogInformation("Email provider not configured — would have sent to {To}: {Subject}", toAddress, subject);
        return Task.FromResult(EmailSendResult.Disabled);
    }
}
