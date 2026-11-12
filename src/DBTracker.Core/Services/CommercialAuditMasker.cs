namespace DBTracker.Core.Services;

/// <summary>Hides commercial figures in audit reasons from roles outside the commercial-evidence policy. New cost-estimate
/// events no longer carry figures; this covers rows written before that change, on screen and in the export.</summary>
public static class CommercialAuditMasker
{
    public const string HiddenText = "Cost figures hidden for your role.";

    private static readonly HashSet<string> CommercialActions = new(StringComparer.Ordinal) { "EditionCostEstimateRecorded" };

    public static string Reason(string action, string reason, bool canSeeCommercialEvidence) =>
        canSeeCommercialEvidence || !CommercialActions.Contains(action) ? reason : HiddenText;

    /// <summary>True when the event's before/after values must be hidden from this viewer, for the same reason as its text.</summary>
    public static bool HidesChange(string action, bool canSeeCommercialEvidence) =>
        !canSeeCommercialEvidence && CommercialActions.Contains(action);
}
