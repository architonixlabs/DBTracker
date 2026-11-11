namespace DBTracker.Infrastructure.Notifications;

/// <summary>Email:Provider selects Smtp (Gmail, Office365, any relay) or ArxMail. The default, None, only logs, so dev and test environments never email anyone.</summary>
public interface IEmailSender
{
    Task<EmailSendResult> SendAsync(string toAddress, string subject, string body, CancellationToken ct = default);
}
