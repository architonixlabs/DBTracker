using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>A restore performed from a backup — DR drill, incident, data request or scheduled test. Evidence that a backup is actually recoverable, not just taken.</summary>
public class RestoreEvent
{
    public int Id { get; set; }

    public int DatabaseInstanceId { get; set; }
    public DatabaseInstance? DatabaseInstance { get; set; }

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Who actually performed the restore — free text, may differ from the logged-in user entering the record (U11).</summary>
    public string PerformedBy { get; set; } = string.Empty;
    public RestoreReason Reason { get; set; } = RestoreReason.Test;
    public RestoreOutcome Outcome { get; set; } = RestoreOutcome.Success;
    public string Notes { get; set; } = string.Empty;

    /// <summary>The authenticated user who entered this record (U11) — distinct from PerformedBy.</summary>
    public string RecordedByUserName { get; set; } = string.Empty;

    /// <summary>The backup evidence this restore validated, when known (U12). Null means "not linked", shown explicitly rather than implying no evidence exists.</summary>
    public int? LinkedBackupRecordId { get; set; }
    public BackupRecord? LinkedBackupRecord { get; set; }

    public double? DurationMinutes { get; set; }
    public double? TargetRecoveryTimeMinutes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<RestoreEventCorrection> Corrections { get; set; } = new List<RestoreEventCorrection>();
}
