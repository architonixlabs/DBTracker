namespace DBTracker.Infrastructure.Notifications;

/// <summary>
/// Email provider is deliberately not locked to one vendor — configure "Email:Provider" to Smtp
/// (Gmail, Office365, any SMTP relay) or ArxMail (the org's HTTP gateway). Defaults to None, which
/// logs instead of sending, so a fresh dev/test environment never accidentally emails anyone.
/// </summary>
public interface IEmailSender
{
    Task<EmailSendResult> SendAsync(string toAddress, string subject, string body, CancellationToken ct = default);
}
