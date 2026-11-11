namespace DBTracker.Core.Enums;

/// <summary>
/// D21 — whether a product/deployment currently has active paid support entitlement on file. This is
/// deliberately a separate dimension from D02/D03's SupportPhase (vendor lifecycle fact): a product
/// can be in Mainstream support with no contract on file (entitlement Unknown/NoContractOnFile), or
/// out of Mainstream support with Active paid Extended coverage. Neither status is derived from the
/// other.
/// </summary>
public enum EntitlementStatus
{
    /// <summary>No support contract has been linked to this instance at all — not the same claim as Expired.</summary>
    NoContractOnFile,
    Active,
    ExpiringSoon,
    Expired
}
