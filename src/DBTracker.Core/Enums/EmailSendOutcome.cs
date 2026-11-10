namespace DBTracker.Core.Enums;

/// <summary>
/// What actually happened when a report/alert email was handed to a provider (U09) — kept distinct
/// from "the recipient received it", which this app has no way to confirm.
/// </summary>
public enum EmailSendOutcome
{
    /// <summary>No provider configured (Email:Provider=None, or a configured provider missing required settings) — nothing was attempted.</summary>
    Disabled,

    /// <summary>The provider (SMTP server or ArxMail gateway) accepted the message for delivery.</summary>
    Accepted,

    /// <summary>The provider rejected the message or the attempt threw — see Detail.</summary>
    Failed
}
