using DBTracker.Core.Enums;

namespace DBTracker.Infrastructure.Notifications;

/// <summary>What a send attempt resulted in. Provider acceptance is never treated as delivery; Detail explains a failure.</summary>
public record EmailSendResult(EmailSendOutcome Outcome, string? Detail = null)
{
    public static readonly EmailSendResult Disabled = new(EmailSendOutcome.Disabled);
    public static readonly EmailSendResult Accepted = new(EmailSendOutcome.Accepted);
    public static EmailSendResult Failed(string detail) => new(EmailSendOutcome.Failed, detail);
}
