namespace DBTracker.Core.Entities;

/// <summary>
/// D22 — one decommission/source-retirement checklist for an instance, optionally tied to the D10
/// MigrationCampaign that replaced it. The computed gates (consumers/dependencies, jobs/replicas,
/// final recoverable backup/key evidence, rollback-window closure, and the two D21-adjacent/sign-off
/// gates below) are evaluated live by RetirementChecklistGateEvaluator from other epics' own records —
/// nothing here duplicates that evidence. Only the genuinely manual facts live as fields on this row:
/// legal-hold review, application/platform sign-off, an externally performed cleanup note, and the
/// cost/contract review flag. RetiredAtUtc/RetiredBy is stamped only after RetirementChecklistService
/// actually calls the existing U14 InstanceArchiveService — this entity never archives anything itself.
/// </summary>
public class RetirementChecklist
{
    public int Id { get; set; }

    public int DatabaseInstanceId { get; set; }
    public DatabaseInstance? DatabaseInstance { get; set; }

    /// <summary>Optional link to the D10 campaign that replaced this instance, if any — a standalone decommission (no migration campaign) is equally valid.</summary>
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

    /// <summary>D21 cost/contract review — reviewer confirms the entitlement/cost facts have actually been looked at before retiring, not that coverage is necessarily active.</summary>
    public bool CostContractReviewed { get; set; }
    public string CostContractReviewNotes { get; set; } = string.Empty;

    /// <summary>Free-text note on where/how the final recoverable backup/key-access snapshot was retained, alongside the computed D16/D17/D20 evidence check.</summary>
    public string FinalBackupEvidenceNotes { get; set; } = string.Empty;

    public DateTime? RetiredAtUtc { get; set; }
    public string RetiredBy { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string UpdatedBy { get; set; } = string.Empty;
}
