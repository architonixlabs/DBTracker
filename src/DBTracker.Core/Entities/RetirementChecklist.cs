namespace DBTracker.Core.Entities;

/// <summary>A decommission checklist for one instance. Gates are evaluated live from other records; only manual facts (legal hold, sign-off, cleanup note, cost review) live here, and retiring goes through InstanceArchiveService.</summary>
public class RetirementChecklist
{
    public int Id { get; set; }

    public int DatabaseInstanceId { get; set; }
    public DatabaseInstance? DatabaseInstance { get; set; }

    /// <summary>Optional campaign that replaced this instance; a standalone decommission is equally valid.</summary>
    public int? MigrationCampaignId { get; set; }
    public MigrationCampaign? MigrationCampaign { get; set; }

    public bool LegalHoldReviewed { get; set; }
    public string LegalHoldReviewNotes { get; set; } = string.Empty;

    public string AppOwnerSignoffBy { get; set; } = string.Empty;
    public DateTime? AppOwnerSignoffAtUtc { get; set; }

    public string PlatformSignoffBy { get; set; } = string.Empty;
    public DateTime? PlatformSignoffAtUtc { get; set; }

    /// <summary>Note describing any externally performed access-revocation/monitoring-cleanup (the tracker doesn't execute this itself).</summary>
    public string ExternalCleanupNotes { get; set; } = string.Empty;

    /// <summary>Reviewer confirms entitlement and cost facts were checked before retiring, not that coverage is active.</summary>
    public bool CostContractReviewed { get; set; }
    public string CostContractReviewNotes { get; set; } = string.Empty;

    /// <summary>Where the final recoverable backup and key-access evidence was retained.</summary>
    public string FinalBackupEvidenceNotes { get; set; } = string.Empty;

    public DateTime? RetiredAtUtc { get; set; }
    public string RetiredBy { get; set; } = string.Empty;

    /// <summary>Set when a checklist is abandoned (started by mistake, or the database is staying). Kept for history; a cancelled checklist no longer blocks a plain archive.</summary>
    public DateTime? CancelledAtUtc { get; set; }
    public string CancelledBy { get; set; } = string.Empty;
    public string CancelReason { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string UpdatedBy { get; set; } = string.Empty;
}
