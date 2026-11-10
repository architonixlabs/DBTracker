namespace DBTracker.Core.Entities;

/// <summary>
/// D19 — a migration space estimate for one instance, optionally linked to a D10 MigrationCampaign
/// and/or a D08 PathAssessment rather than existing in isolation. AssumptionsText is required by
/// convention (enforced at the service layer, not the database) because this is never meant to read
/// as a bare, unexplained number — CapacityForecastBuilder applies the same "show your assumptions"
/// rule to the related growth-forecast computation.
/// </summary>
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
