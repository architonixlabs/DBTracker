namespace DBTracker.Core.Entities;

/// <summary>A pre/post workload comparison referencing two CapacityMeasurement rows. Either side may be null while only one is measured.</summary>
public class WorkloadComparison
{
    public int Id { get; set; }

    public int DatabaseInstanceId { get; set; }
    public DatabaseInstance? DatabaseInstance { get; set; }

    public int? MigrationCampaignId { get; set; }
    public MigrationCampaign? MigrationCampaign { get; set; }

    public int? PreMeasurementId { get; set; }
    public CapacityMeasurement? PreMeasurement { get; set; }

    public int? PostMeasurementId { get; set; }
    public CapacityMeasurement? PostMeasurement { get; set; }

    public string ComparisonNotes { get; set; } = string.Empty;

    public DateTime RecordedAtUtc { get; set; } = DateTime.UtcNow;
    public string RecordedBy { get; set; } = string.Empty;
}
