namespace DBTracker.Core.Enums;

/// <summary>Coverage of an instance's required backup-chain members. Unknown is the answer whenever requirements or evidence can't be checked, never a false Complete.</summary>
public enum ChainCoverageStatus
{
    /// <summary>No chain requirements recorded, or not enough to evaluate. Never treated as green.</summary>
    Unknown,

    /// <summary>A required member has no qualifying evidence. A fresh log/WAL/binlog backup without a valid base is never Complete.</summary>
    Incomplete,

    /// <summary>Every required chain member has qualifying evidence, anchored by a satisfied base.</summary>
    Complete
}
