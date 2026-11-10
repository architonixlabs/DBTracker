using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>
/// D13 — post-change validation for one instance within a D10 MigrationCampaign. This is the record
/// ChangeCompletionGateEvaluator reads before a campaign is allowed to reach CampaignState.Completed —
/// a bare edit of DatabaseInstance.Version is never, on its own, evidence any of this passed.
/// NewBackupRestoreEvidenceOutcome plus LinkedBackupRecordId/LinkedRestoreEventId is the "new
/// successful backup/restore-evidence requirement" from the acceptance criteria, reusing U01/U11/U12's
/// existing evidence rather than inventing a parallel one.
/// </summary>
public class ChangeValidation
{
    public int Id { get; set; }

    public int MigrationCampaignId { get; set; }
    public MigrationCampaign? MigrationCampaign { get; set; }

    public int DatabaseInstanceId { get; set; }
    public DatabaseInstance? DatabaseInstance { get; set; }

    public string ObservedTargetBuildText { get; set; } = string.Empty;
    /// <summary>Reuses D01's compatibility-level concept by name/shape, not a new rule — the text actually observed on the target after the change.</summary>
    public string ObservedCompatibilityLevel { get; set; } = string.Empty;

    public EvidenceCheckOutcome ApplicationTestsOutcome { get; set; } = EvidenceCheckOutcome.NotPerformed;
    public string ApplicationTestsNotes { get; set; } = string.Empty;

    public EvidenceCheckOutcome IntegrityCheckOutcome { get; set; } = EvidenceCheckOutcome.NotPerformed;
    public string IntegrityCheckNotes { get; set; } = string.Empty;

    /// <summary>Representative performance comparison against the documented pre-change baseline.</summary>
    public string PerformanceComparisonNotes { get; set; } = string.Empty;

    public EvidenceCheckOutcome JobsPermissionsOutcome { get; set; } = EvidenceCheckOutcome.NotPerformed;
    public EvidenceCheckOutcome HaReplicationStateOutcome { get; set; } = EvidenceCheckOutcome.NotPerformed;

    public EvidenceCheckOutcome NewBackupRestoreEvidenceOutcome { get; set; } = EvidenceCheckOutcome.NotPerformed;
    public int? LinkedBackupRecordId { get; set; }
    public BackupRecord? LinkedBackupRecord { get; set; }
    public int? LinkedRestoreEventId { get; set; }
    public RestoreEvent? LinkedRestoreEvent { get; set; }

    /// <summary>Optional JSON snapshots of relevant fields before/after, kept for retained before/after evidence.</summary>
    public string? BeforeEvidenceJson { get; set; }
    public string? AfterEvidenceJson { get; set; }

    public DateTime? ValidationTimeUtc { get; set; }
    /// <summary>How long rollback assets (old binaries/backups/replicas) must be preserved after this validation — purely a recorded deadline, nothing here deletes anything automatically.</summary>
    public DateTime? RollbackRetentionDeadlineUtc { get; set; }

    public DateTime RecordedAtUtc { get; set; } = DateTime.UtcNow;
    public string RecordedBy { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<ChangeValidationSignoff> Signoffs { get; set; } = new List<ChangeValidationSignoff>();
}
