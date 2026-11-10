namespace DBTracker.Core.Enums;

/// <summary>
/// The computed coverage of a database instance's required backup-chain members (D16).
/// Unknown — never a false Complete — is the correct answer whenever no chain requirements have
/// been recorded, or evidence can't actually be checked; it is not a "midpoint" between the other
/// two statuses.
/// </summary>
public enum ChainCoverageStatus
{
    /// <summary>No chain requirements recorded for this instance, or the requirement set recorded isn't enough to evaluate — never silently treated as green.</summary>
    Unknown,

    /// <summary>At least one required chain member has no qualifying evidence — a fresh log/WAL/binlog backup existing is never, by itself, enough to call this Complete without a valid base.</summary>
    Incomplete,

    /// <summary>Every required chain member has qualifying evidence, anchored by a satisfied base.</summary>
    Complete
}
