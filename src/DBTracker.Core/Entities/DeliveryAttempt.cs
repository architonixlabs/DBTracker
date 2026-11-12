using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>One send attempt (a report, an alert, a recovery notice or a test email) and its outcome, kept so an admin can diagnose delivery from /setup.</summary>
/// <remarks>Recipient addresses are not stored, only counts, so the history can be shown without exposing the distribution list.</remarks>
public class DeliveryAttempt
{
    public int Id { get; set; }

    public DateTime AttemptedAtUtc { get; set; } = DateTime.UtcNow;

    public DeliveryKind Kind { get; set; }

    /// <summary>"Email" today. Text, so another channel doesn't need a schema change.</summary>
    public string Channel { get; set; } = "Email";

    /// <summary>What was sent: a report scope such as "Full inventory", or the instance an alert was about.</summary>
    public string ScopeDescription { get; set; } = string.Empty;

    public int RecipientCount { get; set; }
    public int AcceptedCount { get; set; }
    public int FailedCount { get; set; }

    /// <summary>Subscribers skipped because their quiet hours were on. Not included in RecipientCount.</summary>
    public int HeldForQuietHoursCount { get; set; }

    public DeliveryOutcome Outcome { get; set; }

    /// <summary>First provider error, trimmed. Empty when nothing failed.</summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>The person who triggered it, or "Background: worker name" for scheduled sends.</summary>
    public string TriggeredBy { get; set; } = string.Empty;
}
