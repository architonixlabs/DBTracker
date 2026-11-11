namespace DBTracker.Core.Entities;

/// <summary>A migration space estimate for one instance. AssumptionsText is required (enforced in the service) so the number never stands unexplained.</summary>
public class MigrationCapacityEstimate
{
    public int Id { get; set; }

    public int DatabaseInstanceId { get; set; }
    public DatabaseInstance? DatabaseInstance { get; set; }

    public int? MigrationCampaignId { get; set; }
    public MigrationCampaign? MigrationCampaign { get; set; }

    public int? PathAssessmentId { get; set; }
    public PathAssessment? PathAssessment { get; set; }

    public double? EstimatedSpaceGb { get; set; }
    public string AssumptionsText { get; set; } = string.Empty;
    public string MissingDataNotes { get; set; } = string.Empty;

    public DateTime EstimatedAtUtc { get; set; } = DateTime.UtcNow;
    public string EstimatedBy { get; set; } = string.Empty;
}
