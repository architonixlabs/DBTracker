namespace DBTracker.Core.Entities;

/// <summary>
/// One upgrade-readiness checklist header (D09) for a specific source/target pairing on an instance —
/// optionally linked to the D08 PathAssessment it follows from. ReadinessChecklistService seeds one
/// ReadinessCheckItem per applicable ReadinessCheckArea at Unknown; nothing here ever defaults an
/// un-performed item to Passed.
/// </summary>
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
