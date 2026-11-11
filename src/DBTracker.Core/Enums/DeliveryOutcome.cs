namespace DBTracker.Core.Enums;

/// <summary>The overall result of one send attempt across all its recipients. Accepted means the provider took it, never that it was delivered.</summary>
public enum DeliveryOutcome
{
    /// <summary>No email provider configured, so nothing was attempted.</summary>
    Disabled,

    /// <summary>Nobody to send to: an empty distribution list, or every recipient held by quiet hours.</summary>
    NoRecipients,

    /// <summary>The provider accepted the message for every recipient.</summary>
    Accepted,

    /// <summary>Some recipients accepted, some failed.</summary>
    Partial,

    /// <summary>Every recipient failed.</summary>
    Failed
}
