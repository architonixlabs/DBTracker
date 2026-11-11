namespace DBTracker.Core.Entities;

/// <summary>An upgrade-readiness checklist for one source/target pairing. Items are seeded at Unknown, never Passed.</summary>
public class ReadinessChecklist
{
    public int Id { get; set; }

    public int DatabaseInstanceId { get; set; }
    public DatabaseInstance? DatabaseInstance { get; set; }

    public int? PathAssessmentId { get; set; }
    public PathAssessment? PathAssessment { get; set; }

    public string SourceLabel { get; set; } = string.Empty;
    public string TargetLabel { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<ReadinessCheckItem> Items { get; set; } = new List<ReadinessCheckItem>();
}
