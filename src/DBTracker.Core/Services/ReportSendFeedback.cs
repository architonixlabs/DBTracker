namespace DBTracker.Core.Services;

/// <param name="Succeeded">True when every recipient was accepted.</param>
public record ReportSendMessage(bool Succeeded, string Text);

/// <summary>What to tell the person who sent a report. Delivery history and the email settings are on the Admin-only
/// Setup page, so only an Admin is pointed there; anyone else is told what to pass on to an administrator.</summary>
public static class ReportSendFeedback
{
    /// <param name="sentAtLabel">The send time in the viewer's zone, for the administrator to find the attempt.</param>
    public static ReportSendMessage Describe(int recipientCount, bool disabled, int acceptedCount, int failedCount, bool viewerIsAdmin, string sentAtLabel)
    {
        if (recipientCount == 0)
        {
            return new(false, viewerIsAdmin
                ? "Not sent: no distribution list is configured. Set Reports:DistributionList, then send again."
                : "Not sent: this report has no recipients yet. Ask an administrator to set the report distribution list, then send again.");
        }

        if (disabled)
        {
            return new(false, viewerIsAdmin
                ? "Not sent: no email provider is configured. Set Email:Provider, then send again."
                : "Not sent: email sending is turned off. Ask an administrator to set up the email provider, then send again.");
        }

        if (failedCount == 0)
        {
            return new(true, acceptedCount == 1 ? "Report accepted for 1 recipient." : $"Report accepted for {acceptedCount} recipients.");
        }

        var what = acceptedCount == 0
            ? $"The report failed to send to all {failedCount} recipient(s)."
            : $"Partial send: {acceptedCount} accepted, {failedCount} failed.";

        return new(false, viewerIsAdmin
            ? $"{what} The error is under Setup and diagnostics, Delivery history."
            : $"{what} Ask an administrator to check the email delivery history for the send at {sentAtLabel}. You can send again once it is fixed.");
    }
}
