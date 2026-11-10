using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>
/// D18 — one recorded/imported result of an integrity check, replication/failover drill, or a
/// required recurring maintenance job. Named generically (CheckType, not "DBCC result") so it covers
/// every engine, not just SQL Server. This is always a point-in-time recorded result — never a
/// continuously measured HA health signal — and is deliberately kept off BackupHealth entirely
/// (U04's "separate concerns" pattern): a Failed/Stale/Missing row here never changes a backup
/// health badge, and vice versa. RelatedDependencyId optionally reuses D07's InstanceDependency to
/// identify which node/deployment this result is about, instead of inventing a parallel topology
/// model; NodeOrDeploymentLabel is the free-text fallback when there's no matching dependency row.
/// </summary>
public class MaintenanceEvidenceRecord
{
    public int Id { get; set; }

    public int DatabaseInstanceId { get; set; }
    public DatabaseInstance? DatabaseInstance { get; set; }

    public MaintenanceCheckType CheckType { get; set; }

    public string NodeOrDeploymentLabel { get; set; } = string.Empty;

    /// <summary>Optional reuse of D07's dependency/topology model for the node/deployment this result covers.</summary>
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
