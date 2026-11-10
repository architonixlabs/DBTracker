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

    /// <summary>Owner team (U21) — a reusable alternative/complement to the free-text Contact* fields above, which are preserved unmodified. Null = no team assigned yet.</summary>
    public int? OwnerTeamId { get; set; }
    public Team? OwnerTeam { get; set; }

    /// <summary>Backup contact (U21) — the primary contact stays whatever ContactName/Email/Phone already held; this is the "beyond one email" second contact the backlog asked for.</summary>
    public string BackupContactName { get; set; } = string.Empty;

    [OptionalEmailAddress]
    public string BackupContactEmail { get; set; } = string.Empty;
    public string BackupContactPhone { get; set; } = string.Empty;

    /// <summary>Ownership confirmed-current attestation (U21) — purely informational; absence never blocks any action, it only flags the instance distinctly on the list/detail pages.</summary>
    public DateTime? OwnershipConfirmedAtUtc { get; set; }
    public string OwnershipConfirmedBy { get; set; } = string.Empty;

    public InstanceRole Role { get; set; } = InstanceRole.Active;
    public EnvironmentTier Environment { get; set; } = EnvironmentTier.Production;

    public string DatabaseIp { get; set; } = string.Empty;
    public string ServerIp { get; set; } = string.Empty;

    /// <summary>Structured topology facets (U21) — alongside the existing free-text Tags field below, not replacing it.</summary>
    public string Region { get; set; } = string.Empty;
    public string Cluster { get; set; } = string.Empty;
    public string ReplicaSet { get; set; } = string.Empty;

    /// <summary>Expected hours between successful backups before this instance is considered overdue.</summary>
    public int ExpectedBackupIntervalHours { get; set; } = 24;

    /// <summary>UNC path or local folder the scanner checks for backup files. Empty = scanning disabled.</summary>
    public string BackupFileLocation { get; set; } = string.Empty;

    /// <summary>Semicolon/comma-separated glob patterns (e.g. "*.bak;*.trn") deciding which files on BackupFileLocation count as evidence. Empty = match every file (pre-U01 behavior).</summary>
    public string BackupFileNamePatterns { get; set; } = string.Empty;
    public DateTime? LastScanUtc { get; set; }
    public ScanStatus LastScanStatus { get; set; } = ScanStatus.NeverScanned;
    public string LastScanError { get; set; } = string.Empty;

    /// <summary>Expected days/copies backups for this instance should be retained — informational, not enforced.</summary>
    public int? RetentionPolicyDays { get; set; }

    /// <summary>Per-instance override of the global restore-test interval (RestoreTest:IntervalDays), in days. Null = use the global default (U12).</summary>
    public int? RestoreTestIntervalDaysOverride { get; set; }
    public string Tags { get; set; } = string.Empty;
    public string RunbookUrl { get; set; } = string.Empty;

    /// <summary>Which reusable BackupPolicy (U21) was last applied to this instance's cadence/retention/restore-test fields, and at which policy Version — purely informational provenance. The instance's own fields (above) remain the live source of truth for health/alerting; editing them afterward is a per-instance override that this pointer doesn't prevent.</summary>
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

    /// <summary>Archive attribution (U14) — who/when/why, recorded alongside the audit log so the instance detail page can show it even before anyone opens /audit.</summary>
    public DateTime? ArchivedAtUtc { get; set; }
    public string ArchivedBy { get; set; } = string.Empty;
    public string ArchiveReason { get; set; } = string.Empty;

    /// <summary>Optional host/DNS override for the connectivity check (U18) — leave blank to use ServerIp/DatabaseIp.</summary>
    public string ConnectivityHostOverride { get; set; } = string.Empty;

    /// <summary>Optional port override for the connectivity check (U18) — null uses the engine's conventional default port.</summary>
    public int? ConnectivityPortOverride { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>SQL Server ROWVERSION concurrency token (U13) — lets the editor detect another session saved first, instead of silently overwriting newer changes.</summary>
    public byte[] RowVersion { get; set; } = [];

    public ICollection<BackupRecord> BackupRecords { get; set; } = new List<BackupRecord>();
    public ICollection<RestoreEvent> RestoreEvents { get; set; } = new List<RestoreEvent>();
}
