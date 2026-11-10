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

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
}
