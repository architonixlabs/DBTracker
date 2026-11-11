using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

public record DeliveryRecipientResult(EmailSendOutcome Outcome, string? Detail = null);

public record DeliverySummary(DeliveryOutcome Outcome, int RecipientCount, int AcceptedCount, int FailedCount, string Error);

/// <summary>Rolls per-recipient send outcomes into one attempt outcome. A disabled sender is never summarized as accepted.</summary>
public static class DeliveryOutcomeSummarizer
{
    public const int MaxErrorLength = 1000;

    public static DeliverySummary Summarize(IReadOnlyCollection<DeliveryRecipientResult> results)
    {
        var accepted = results.Count(r => r.Outcome == EmailSendOutcome.Accepted);
        var failed = results.Count(r => r.Outcome == EmailSendOutcome.Failed);
        var disabled = results.Count(r => r.Outcome == EmailSendOutcome.Disabled);

        var outcome =
            results.Count == 0 ? DeliveryOutcome.NoRecipients
            : disabled == results.Count ? DeliveryOutcome.Disabled
            : failed == 0 ? DeliveryOutcome.Accepted
            : accepted == 0 ? DeliveryOutcome.Failed
            : DeliveryOutcome.Partial;

        var error = results.FirstOrDefault(r => r.Outcome == EmailSendOutcome.Failed)?.Detail ?? "";
        if (failed > 1)
        {
            error = $"{failed} recipients failed. First error: {error}";
        }

        return new DeliverySummary(outcome, results.Count, accepted, failed, Trim(error));
    }

    public static string Describe(DeliveryOutcome outcome) => outcome switch
    {
        DeliveryOutcome.Disabled => "Disabled, not sent",
        DeliveryOutcome.NoRecipients => "No recipients",
        DeliveryOutcome.Accepted => "Accepted by provider",
        DeliveryOutcome.Partial => "Partly failed",
        DeliveryOutcome.Failed => "Failed",
        _ => outcome.ToString()
    };

    public static string Describe(DeliveryKind kind) => kind switch
    {
        DeliveryKind.ComplianceReport => "Compliance report",
        DeliveryKind.Alert => "Alert",
        DeliveryKind.Recovery => "Recovery notice",
        DeliveryKind.TestEmail => "Test email",
        _ => kind.ToString()
    };

    private static string Trim(string error) =>
        error.Length <= MaxErrorLength ? error : error[..(MaxErrorLength - 3)] + "...";
}
