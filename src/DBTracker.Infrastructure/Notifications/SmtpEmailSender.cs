using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DBTracker.Infrastructure.Notifications;

/// <summary>Plain SMTP sender — works with Gmail (smtp.gmail.com:587 + an app password), Office365, or any relay.</summary>
public class SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task<EmailSendResult> SendAsync(string toAddress, string subject, string body, CancellationToken ct = default)
    {
        var o = options.Value;
        if (string.IsNullOrWhiteSpace(o.Host) || string.IsNullOrWhiteSpace(o.FromAddress))
        {
            logger.LogWarning("SMTP email provider selected but not fully configured, skipping send to {To}.", toAddress);
            return EmailSendResult.Disabled;
        }

        using var client = new SmtpClient(o.Host, o.Port)
        {
            EnableSsl = o.EnableSsl
        };

        if (!string.IsNullOrWhiteSpace(o.Username))
        {
            client.Credentials = new NetworkCredential(o.Username, o.Password);
        }

        using var message = new MailMessage
        {
            From = new MailAddress(o.FromAddress, o.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };
        message.To.Add(toAddress);

        try
        {
            await client.SendMailAsync(message, ct);
            return EmailSendResult.Accepted;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "SMTP send to {To} failed.", toAddress);
            return EmailSendResult.Failed(ex.Message);
        }
    }
}
