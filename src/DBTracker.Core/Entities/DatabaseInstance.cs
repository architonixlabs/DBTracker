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
    public DateTime? LastScanUtc { get; set; }
    public ScanStatus LastScanStatus { get; set; } = ScanStatus.NeverScanned;
    public string LastScanError { get; set; } = string.Empty;

    /// <summary>Expected days/copies backups for this instance should be retained — informational, not enforced.</summary>
    public int? RetentionPolicyDays { get; set; }
    public string Tags { get; set; } = string.Empty;
    public string RunbookUrl { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;
    public bool IsArchived { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<BackupRecord> BackupRecords { get; set; } = new List<BackupRecord>();
    public ICollection<RestoreEvent> RestoreEvents { get; set; } = new List<RestoreEvent>();
}
