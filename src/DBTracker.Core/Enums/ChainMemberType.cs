namespace DBTracker.Core.Enums;

/// <summary>
/// One kind of backup-chain dependency a recovery can require (D16). Not every engine uses every
/// member — e.g. a WAL-archiving Postgres instance cares about Base + WalArchive, a MySQL instance
/// using binlog replication cares about Base + Binlog, and a point-snapshot engine (e.g. many managed
/// document stores) may require none of these at all. Only the members an instance's
/// BackupChainRequirement rows actually mark IsRequired=true are evaluated for that instance —
/// nothing here forces a chain concept onto an engine that doesn't have one.
/// </summary>
public enum ChainMemberType
{
    /// <summary>A full/base backup — the foundation every other member in the chain depends on.</summary>
    Base,
    Differential,
    TransactionLog,
    WalArchive,
    Binlog,

    /// <summary>Long-horizon archival copy (e.g. a separately retained cold-storage base) required for a recovery window beyond the primary chain's own retention.</summary>
    Archival
}
