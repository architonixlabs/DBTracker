namespace DBTracker.Core.Entities;

/// <summary>
/// A reusable, version-able cadence/retention/restore-test policy (U21) — builds on U12's org-default
/// + per-instance-override pattern by giving the org-level default a name and a version number that
/// bumps on every edit, so instances that were given this policy can later be checked against the
/// policy's *current* version to see whether they've drifted (AppliedPolicyVersion on
/// DatabaseInstance). Applying a policy to an instance copies its values onto that instance's own
/// cadence/retention/restore-test fields — the instance can still be edited afterward (a per-instance
/// override), same as every other field on DatabaseInstance always could be.
/// </summary>
public class BackupPolicy
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Bumped every time this policy's values are edited — a simple, visible version marker, with the actual before/after captured in the U19 audit trail.</summary>
    public int Version { get; set; } = 1;

    public int ExpectedBackupIntervalHours { get; set; } = 24;
    public int? RetentionPolicyDays { get; set; }
    public int? RestoreTestIntervalDays { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string UpdatedBy { get; set; } = string.Empty;
}
