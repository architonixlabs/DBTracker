namespace DBTracker.Core.Entities;

/// <summary>
/// D16 — the business recovery objectives for one instance (one row per instance, like
/// VersionIdentity). RpoTargetMinutes/RtoTargetMinutes are what the business says it needs;
/// BackupChainCoverageCalculator separately computes what the recorded evidence actually supports —
/// nothing here claims RPO compliance from timestamps alone. EncryptionKeyReference/
/// OffsiteCopyReference are reference/flag fields only for this epic; full detail lives in D20
/// (CertificateDependency) and D17 (BackupCopyLocation) respectively.
/// </summary>
public class RecoveryObjective
{
    public int Id { get; set; }

    public int DatabaseInstanceId { get; set; }
    public DatabaseInstance? DatabaseInstance { get; set; }

    public int? RpoTargetMinutes { get; set; }
    public int? RtoTargetMinutes { get; set; }

    /// <summary>Reference only (e.g. "Vault: sql-prod-tde-key") — never the key/secret itself. Full tracking is D20's CertificateDependency; this is a lightweight pointer for this objective's own context.</summary>
    public string EncryptionKeyReferenceNote { get; set; } = string.Empty;

    /// <summary>Reference/flag only — full tracking is D17's BackupCopyLocation.</summary>
    public string OffsiteCopyReferenceNote { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string UpdatedBy { get; set; } = string.Empty;
}
