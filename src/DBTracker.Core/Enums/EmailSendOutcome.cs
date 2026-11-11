namespace DBTracker.Core.Enums;

/// <summary>What happened when an email was handed to the provider. Delivery to the recipient can't be confirmed, so it isn't claimed.</summary>
public enum EmailSendOutcome
{
    /// <summary>No provider configured, or a configured provider is missing required settings. Nothing was attempted.</summary>
    Disabled,

    /// <summary>The provider (SMTP server or ArxMail gateway) accepted the message for delivery.</summary>
    Accepted,

    /// <summary>The provider rejected the message or the attempt threw; see Detail.</summary>
    Failed
}
