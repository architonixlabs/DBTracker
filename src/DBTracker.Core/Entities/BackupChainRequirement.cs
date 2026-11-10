using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>
/// One chain-member type this instance's engine/topology actually requires for a full recovery
/// (D16) — e.g. a WAL-archiving PostgreSQL instance records Base + WalArchive; a MySQL instance
/// replicating via binlog records Base + Binlog; a point-snapshot engine with no chain concept at
/// all simply has no rows here, which is exactly what keeps BackupChainCoverageCalculator honestly
/// Unknown for it rather than inventing a chain requirement that doesn't apply.
/// </summary>
public class BackupChainRequirement
{
    public int Id { get; set; }

    public int DatabaseInstanceId { get; set; }
    public DatabaseInstance? DatabaseInstance { get; set; }

    public ChainMemberType MemberType { get; set; }
    public bool IsRequired { get; set; } = true;

    /// <summary>Maximum age, in hours, a successful record of this member type may be and still count as covering "now" — null uses the instance's own ExpectedBackupIntervalHours.</summary>
    public int? MaxAgeHoursOverride { get; set; }

    public string Notes { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
}
