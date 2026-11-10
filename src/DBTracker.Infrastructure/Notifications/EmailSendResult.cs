using DBTracker.Core.Enums;

namespace DBTracker.Infrastructure.Notifications;

/// <summary>
/// What a send attempt actually resulted in (U09) — "the provider accepted it" is never conflated
/// with "the recipient received it"; Detail carries diagnosis for a Failed attempt.
/// </summary>
public record EmailSendResult(EmailSendOutcome Outcome, string? Detail = null)
{
    public static readonly EmailSendResult Disabled = new(EmailSendOutcome.Disabled);
    public static readonly EmailSendResult Accepted = new(EmailSendOutcome.Accepted);
    public static EmailSendResult Failed(string detail) => new(EmailSendOutcome.Failed, detail);
}
