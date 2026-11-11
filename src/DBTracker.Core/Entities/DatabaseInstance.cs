using System.ComponentModel.DataAnnotations;
using DBTracker.Core.Enums;
using DBTracker.Core.Validation;

namespace DBTracker.Core.Entities;

public class DatabaseInstance
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Instance name is required.")]
    public string Name { get; set; } = string.Empty;
    public DatabaseEngine Engine { get; set; }
    public string Version { get; set; } = string.Empty;

    public string Application { get; set; } = string.Empty;
    public string Company { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;

    public string ContactName { get; set; } = string.Empty;

    [OptionalEmailAddress]
    public string ContactEmail { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;

    /// <summary>Owning team, alongside the free-text Contact* fields. Null means no team assigned.</summary>
    public int? OwnerTeamId { get; set; }
    public Team? OwnerTeam { get; set; }

    /// <summary>Second contact; the primary contact stays in ContactName/Email/Phone.</summary>
    public string BackupContactName { get; set; } = string.Empty;

    [OptionalEmailAddress]
    public string BackupContactEmail { get; set; } = string.Empty;
    public string BackupContactPhone { get; set; } = string.Empty;

    /// <summary>When ownership was last confirmed as current. Informational only; it never blocks an action.</summary>
    public DateTime? OwnershipConfirmedAtUtc { get; set; }
    public string OwnershipConfirmedBy { get; set; } = string.Empty;

    public InstanceRole Role { get; set; } = InstanceRole.Active;
    public EnvironmentTier Environment { get; set; } = EnvironmentTier.Production;

    /// <summary>Business criticality, independent of Environment. Unknown is the default and is never inferred.</summary>
    public InstanceCriticality Criticality { get; set; } = InstanceCriticality.Unknown;

    public string DatabaseIp { get; set; } = string.Empty;
    public string ServerIp { get; set; } = string.Empty;

    /// <summary>Structured topology facets, in addition to the free-text Tags field.</summary>
    public string Region { get; set; } = string.Empty;
    public string Cluster { get; set; } = string.Empty;
    public string ReplicaSet { get; set; } = string.Empty;

    /// <summary>Expected hours between successful backups before this instance is considered overdue.</summary>
    public int ExpectedBackupIntervalHours { get; set; } = 24;

    /// <summary>UNC path or local folder the scanner checks for backup files. Empty = scanning disabled.</summary>
    public string BackupFileLocation { get; set; } = string.Empty;

    /// <summary>Semicolon/comma-separated glob patterns (e.g. "*.bak;*.trn") for files that count as evidence. Empty matches every file.</summary>
    public string BackupFileNamePatterns { get; set; } = string.Empty;
    public DateTime? LastScanUtc { get; set; }
    public ScanStatus LastScanStatus { get; set; } = ScanStatus.NeverScanned;
    public string LastScanError { get; set; } = string.Empty;

    /// <summary>Expected days/copies backups for this instance should be retained — informational, not enforced.</summary>
    public int? RetentionPolicyDays { get; set; }

    /// <summary>Per-instance override of RestoreTest:IntervalDays, in days. Null uses the global default.</summary>
    public int? RestoreTestIntervalDaysOverride { get; set; }
    public string Tags { get; set; } = string.Empty;
    public string RunbookUrl { get; set; } = string.Empty;

    /// <summary>Which BackupPolicy and version were last applied. Provenance only; the instance's own fields stay the source of truth.</summary>
    public int? AppliedPolicyId { get; set; }
    public BackupPolicy? AppliedPolicy { get; set; }
    public int? AppliedPolicyVersion { get; set; }

    /// <summary>The health this instance was last alerted on (Overdue/Failed), so alerts fire on a state change, not every sweep.</summary>
    public BackupHealth? LastNotifiedHealth { get; set; }
    public DateTime? LastNotifiedUtc { get; set; }

    /// <summary>Planned maintenance window — while now is before this, Overdue/Failed alerts (email + webhook) are suppressed. The dashboard/detail health badge itself stays truthful.</summary>
    public DateTime? MaintenanceUntilUtc { get; set; }
    public string MaintenanceReason { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;
    public bool IsArchived { get; set; }

    /// <summary>Who archived the instance, when, and why, shown on the detail page without opening the audit log.</summary>
    public DateTime? ArchivedAtUtc { get; set; }
    public string ArchivedBy { get; set; } = string.Empty;
    public string ArchiveReason { get; set; } = string.Empty;

    /// <summary>Optional host/DNS override for the connectivity check. Blank uses ServerIp/DatabaseIp.</summary>
    public string ConnectivityHostOverride { get; set; } = string.Empty;

    /// <summary>Optional port override for the connectivity check. Null uses the engine's default port.</summary>
    public int? ConnectivityPortOverride { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>ROWVERSION concurrency token, so the editor detects a newer save instead of overwriting it.</summary>
    public byte[] RowVersion { get; set; } = [];

    public ICollection<BackupRecord> BackupRecords { get; set; } = new List<BackupRecord>();
    public ICollection<RestoreEvent> RestoreEvents { get; set; } = new List<RestoreEvent>();
}
