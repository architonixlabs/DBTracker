namespace DBTracker.Core.Enums;

/// <summary>A backup-chain dependency a recovery can require. Only members an instance marks IsRequired are evaluated, so engines without a chain concept aren't forced into one.</summary>
public enum ChainMemberType
{
    /// <summary>A full/base backup that every other member depends on.</summary>
    Base,
    Differential,
    TransactionLog,
    WalArchive,
    Binlog,

    /// <summary>Long-horizon archival copy (e.g. a separately retained cold-storage base) required for a recovery window beyond the primary chain's own retention.</summary>
    Archival
}
