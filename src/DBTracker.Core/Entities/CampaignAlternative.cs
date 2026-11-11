using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>
/// One alternative recorded and compared on a D10 MigrationCampaign — upgrade, patch, replace,
/// retire or a documented support-extension/exception. IsChosen marks which one the campaign is
/// actually proceeding with; recording the others preserves the comparison instead of only keeping
/// the final decision.
/// </summary>
public class CampaignAlternative
{
    public int Id { get; set; }

    public int MigrationCampaignId { get; set; }
    public MigrationCampaign? MigrationCampaign { get; set; }

    public CampaignAlternativeType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal? EstimatedEffortHours { get; set; }
    public decimal? EstimatedCost { get; set; }
    public string RiskNotes { get; set; } = string.Empty;
    public bool IsChosen { get; set; }

    /// <summary>D21 feed — optional link to the reviewed EditionCostEstimate this alternative's EstimatedCost came from, kept for traceability. Never auto-syncs after the fact; EstimatedCost stays the authoritative, explicitly-entered figure on this row.</summary>
    public int? SourceCostEstimateId { get; set; }
    public EditionCostEstimate? SourceCostEstimate { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
}
