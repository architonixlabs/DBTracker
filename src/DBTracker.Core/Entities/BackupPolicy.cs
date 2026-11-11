namespace DBTracker.Core.Entities;

/// <summary>A named, versioned cadence/retention/restore-test policy. Applying it copies values onto the instance, which can still be edited afterward; AppliedPolicyVersion shows drift from the current version.</summary>
public class BackupPolicy
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Bumped on every edit; the before/after values live in the audit trail.</summary>
    public int Version { get; set; } = 1;

    public int ExpectedBackupIntervalHours { get; set; } = 24;
    public int? RetentionPolicyDays { get; set; }
    public int? RestoreTestIntervalDays { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string UpdatedBy { get; set; } = string.Empty;
}
