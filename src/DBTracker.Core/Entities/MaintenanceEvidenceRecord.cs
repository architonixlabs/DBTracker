using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>A recorded integrity check, failover drill, or maintenance job result. Point-in-time only, and kept apart from backup health in both directions.</summary>
public class MaintenanceEvidenceRecord
{
    public int Id { get; set; }

    public int DatabaseInstanceId { get; set; }
    public DatabaseInstance? DatabaseInstance { get; set; }

    public MaintenanceCheckType CheckType { get; set; }

    public string NodeOrDeploymentLabel { get; set; } = string.Empty;

    /// <summary>Optional dependency row identifying the node or deployment this result covers.</summary>
    public int? RelatedDependencyId { get; set; }
    public InstanceDependency? RelatedDependency { get; set; }

    public string ToolName { get; set; } = string.Empty;
    public string ToolVersion { get; set; } = string.Empty;
    public string ScheduleDescription { get; set; } = string.Empty;

    /// <summary>Expected hours between runs, for staleness classification — null means no schedule to compare against (MaintenanceEvidenceStalenessClassifier returns Unknown staleness, never a false Current).</summary>
    public int? ExpectedIntervalHours { get; set; }

    public DateTime? RanAtUtc { get; set; }

    public MaintenanceEvidenceSource Source { get; set; } = MaintenanceEvidenceSource.ManuallyEntered;

    /// <summary>URL/ticket id when Source isn't ManuallyEntered — a reviewed external monitoring/ticket link is a first-class evidence source here, not a fallback.</summary>
    public string SourceReference { get; set; } = string.Empty;

    public MaintenanceOutcome Outcome { get; set; } = MaintenanceOutcome.Unknown;
    public string Notes { get; set; } = string.Empty;

    public DateTime RecordedAtUtc { get; set; } = DateTime.UtcNow;
    public string RecordedBy { get; set; } = string.Empty;
}
