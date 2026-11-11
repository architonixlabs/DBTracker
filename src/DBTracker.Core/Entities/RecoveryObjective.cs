namespace DBTracker.Core.Entities;

/// <summary>The business RPO/RTO targets for one instance. BackupChainCoverageCalculator computes what the evidence actually supports; nothing claims compliance from timestamps alone.</summary>
public class RecoveryObjective
{
    public int Id { get; set; }

    public int DatabaseInstanceId { get; set; }
    public DatabaseInstance? DatabaseInstance { get; set; }

    public int? RpoTargetMinutes { get; set; }
    public int? RtoTargetMinutes { get; set; }

    /// <summary>Reference only (e.g. "Vault: sql-prod-tde-key"), never the key itself. Full tracking lives in CertificateDependency.</summary>
    public string EncryptionKeyReferenceNote { get; set; } = string.Empty;

    /// <summary>Reference only; full tracking lives in BackupCopyLocation.</summary>
    public string OffsiteCopyReferenceNote { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string UpdatedBy { get; set; } = string.Empty;
}
