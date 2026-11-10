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

    public InstanceRole Role { get; set; } = InstanceRole.Active;
    public EnvironmentTier Environment { get; set; } = EnvironmentTier.Production;

    public string DatabaseIp { get; set; } = string.Empty;
    public string ServerIp { get; set; } = string.Empty;

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
    public string Tags { get; set; } = string.Empty;
    public string RunbookUrl { get; set; } = string.Empty;

    /// <summary>The health this instance was last alerted on (Overdue/Failed), so alerts fire on a state change, not every sweep.</summary>
    public BackupHealth? LastNotifiedHealth { get; set; }
    public DateTime? LastNotifiedUtc { get; set; }

    /// <summary>Planned maintenance window — while now is before this, Overdue/Failed alerts (email + webhook) are suppressed. The dashboard/detail health badge itself stays truthful.</summary>
    public DateTime? MaintenanceUntilUtc { get; set; }
    public string MaintenanceReason { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;
    public bool IsArchived { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<BackupRecord> BackupRecords { get; set; } = new List<BackupRecord>();
    public ICollection<RestoreEvent> RestoreEvents { get; set; } = new List<RestoreEvent>();
}
