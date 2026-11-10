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

    /// <summary>Exact file size in bytes at discovery time, for Discovered records — the dedup signature (with SourceFile) that lets a changed file reusing the same name be recorded as new evidence rather than silently skipped or duplicated. Null for Manual records and for Discovered records predating U01.</summary>
    public long? ObservedFileBytes { get; set; }

    /// <summary>
    /// The authenticated user who entered this record (U11) — distinct from TimestampUtc, which is
    /// when the backup actually occurred and may be backdated after an incident. Empty for
    /// system-generated Discovered records (there's no human "entry").
    /// </summary>
    public string RecordedByUserName { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Which backup-chain member (D16) this record actually satisfies, when it's something
    /// BackupType alone can't express (WAL archive, binlog, a separate archival copy). Null means
    /// "map from Type" (Full→Base, Differential→Differential, TransactionLog→TransactionLog) —
    /// BackupChainCoverageCalculator applies that fallback itself, so existing/legacy records never
    /// need backfilling for chain coverage to work.
    /// </summary>
    public ChainMemberType? ChainRole { get; set; }

    public ICollection<BackupRecordCorrection> Corrections { get; set; } = new List<BackupRecordCorrection>();
}
