using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>
/// D11 — one rehearsal of a migration method against a named source/target, on a representative
/// dataset, for one instance within a D10 MigrationCampaign. Reuses U11/U12 restore evidence via
/// LinkedRestoreEventId rather than duplicating the restore-event model — the rehearsal's own
/// BackupRestoreOutcome records whether that restore (if any) actually succeeded as part of this
/// rehearsal. PostCutoverWriteDisposition/PostCutoverWriteReconciliationPlan exist specifically so a
/// rollback is never implied to preserve post-cutover writes automatically — that must be an explicit,
/// documented decision. RehearsalEvidenceEvaluator (pure) is what decides freshness/pass-fail from
/// these fields; nothing here is auto-computed.
/// </summary>
public class RehearsalRecord
{
    public int Id { get; set; }

    public int MigrationCampaignId { get; set; }
    public MigrationCampaign? MigrationCampaign { get; set; }

    public int DatabaseInstanceId { get; set; }
    public DatabaseInstance? DatabaseInstance { get; set; }

    /// <summary>Optional traceability back to the D08 path assessment this rehearsal exercises — never required, since a rehearsal can precede a formal assessment.</summary>
    public int? PathAssessmentId { get; set; }
    public PathAssessment? PathAssessment { get; set; }

    public string SourceLabel { get; set; } = string.Empty;
    public string TargetLabel { get; set; } = string.Empty;
    public string RepresentativeDatasetDescription { get; set; } = string.Empty;
    public MigrationMethod Method { get; set; }

    public DateTime PerformedAtUtc { get; set; } = DateTime.UtcNow;
    public string PerformedBy { get; set; } = string.Empty;
    public double? DurationMinutes { get; set; }

    public EvidenceCheckOutcome BackupRestoreOutcome { get; set; } = EvidenceCheckOutcome.NotPerformed;
    /// <summary>The specific U11/U12 restore evidence this rehearsal's backup-restore step relied on, when known — reused, not duplicated.</summary>
    public int? LinkedRestoreEventId { get; set; }
    public RestoreEvent? LinkedRestoreEvent { get; set; }

    public EvidenceCheckOutcome CorrectnessCheckOutcome { get; set; } = EvidenceCheckOutcome.NotPerformed;
    public string CorrectnessCheckNotes { get; set; } = string.Empty;

    public EvidenceCheckOutcome PerformanceCheckOutcome { get; set; } = EvidenceCheckOutcome.NotPerformed;
    /// <summary>Documented baseline vs. the measured result for this rehearsal — a comparison, not a bare pass/fail flag.</summary>
    public string PerformanceComparisonNotes { get; set; } = string.Empty;

    public double? TargetRpoMinutes { get; set; }
    public double? TargetRtoMinutes { get; set; }
    public double? MeasuredRpoMinutes { get; set; }
    public double? MeasuredRtoMinutes { get; set; }

    public string PrerequisitesText { get; set; } = string.Empty;
    /// <summary>Encryption-key/certificate/other recovery dependencies this rehearsal needed — surfaced explicitly rather than assumed available.</summary>
    public string RecoveryKeyDependenciesText { get; set; } = string.Empty;

    public PostCutoverWriteDisposition PostCutoverWriteDisposition { get; set; } = PostCutoverWriteDisposition.NotApplicable;
    /// <summary>Required whenever PostCutoverWriteDisposition isn't NotApplicable — how writes landing after cutover would actually be reconciled or accepted as lost on a rollback.</summary>
    public string PostCutoverWriteReconciliationPlan { get; set; } = string.Empty;

    /// <summary>Per-rehearsal override of the org-wide default validity window (days) — null uses the configured default.</summary>
    public int? ValidityDays { get; set; }

    public DateTime RecordedAtUtc { get; set; } = DateTime.UtcNow;
    public string RecordedBy { get; set; } = string.Empty;

    public ICollection<RehearsalRollbackDecisionPoint> RollbackDecisionPoints { get; set; } = new List<RehearsalRollbackDecisionPoint>();
}
