namespace DBTracker.Core.Entities;

/// <summary>
/// D19 — a pre/post representative-workload comparison for one instance's migration, reusing two
/// existing CapacityMeasurement rows (Workload or PerformanceBaseline type) rather than duplicating
/// their values. Either side may be null while only one half has been measured yet — the comparison
/// is honest about being partial rather than forcing both sides to exist before anything can be
/// recorded.
/// </summary>
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
