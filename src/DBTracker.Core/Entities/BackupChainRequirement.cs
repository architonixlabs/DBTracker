using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>A chain member (e.g. Base + WalArchive) this instance needs for full recovery. Engines with no chain concept have no rows, which keeps coverage Unknown instead of invented.</summary>
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
