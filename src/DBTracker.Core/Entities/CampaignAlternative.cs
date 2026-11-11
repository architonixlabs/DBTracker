using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>One option compared on a migration campaign (upgrade, patch, replace, retire, exception). Keeping the rejected ones preserves the comparison; IsChosen marks the one in progress.</summary>
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

    /// <summary>Optional link to the cost estimate EstimatedCost came from, for traceability. Never auto-synced; EstimatedCost stays authoritative.</summary>
    public int? SourceCostEstimateId { get; set; }
    public EditionCostEstimate? SourceCostEstimate { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
}
