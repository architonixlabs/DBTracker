namespace DBTracker.Core.Entities;

/// <summary>The storage limit a capacity forecast compares growth against, saved per instance with where the figure came from.</summary>
public class InstanceCapacityLimit
{
    public int Id { get; set; }

    public int DatabaseInstanceId { get; set; }
    public DatabaseInstance? DatabaseInstance { get; set; }

    public double CapacityLimitGb { get; set; }

    /// <summary>Where the limit comes from, e.g. "SAN volume quota" or "RDS allocated storage".</summary>
    public string Source { get; set; } = string.Empty;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string UpdatedBy { get; set; } = string.Empty;
}
