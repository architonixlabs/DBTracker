using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>Post-change validation for one instance in a campaign. The completion gate reads this; editing DatabaseInstance.Version alone never counts as evidence.</summary>
public class ChangeValidation
{
    public int Id { get; set; }

    public int MigrationCampaignId { get; set; }
    public MigrationCampaign? MigrationCampaign { get; set; }

    public int DatabaseInstanceId { get; set; }
    public DatabaseInstance? DatabaseInstance { get; set; }

    public string ObservedTargetBuildText { get; set; } = string.Empty;
    /// <summary>The compatibility level actually observed on the target after the change.</summary>
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
