using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

public class BackupRecord
{
    public int Id { get; set; }

    public int DatabaseInstanceId { get; set; }
    public DatabaseInstance? DatabaseInstance { get; set; }

    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public BackupType Type { get; set; } = BackupType.Full;
    public BackupStatus Status { get; set; } = BackupStatus.Success;

    public double? SizeMb { get; set; }
    public double? DurationMinutes { get; set; }

    public string Notes { get; set; } = string.Empty;

    public BackupSource Source { get; set; } = BackupSource.Manual;

    /// <summary>File name matched on the shared drive, for Discovered records — used to avoid re-importing the same file.</summary>
    public string SourceFile { get; set; } = string.Empty;

    /// <summary>Exact byte size at discovery. With SourceFile it is the dedup signature, so a changed file reusing a name counts as new evidence. Null for Manual records and older Discovered records.</summary>
    public long? ObservedFileBytes { get; set; }

    /// <summary>The authenticated user who entered this record. TimestampUtc is when the backup occurred and may be backdated. Empty for Discovered records.</summary>
    public string RecordedByUserName { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>The chain member this record satisfies when Type can't express it (WAL, binlog, archival copy). Null maps from Type, so older records need no backfill.</summary>
    public ChainMemberType? ChainRole { get; set; }

    public ICollection<BackupRecordCorrection> Corrections { get; set; } = new List<BackupRecordCorrection>();
}
