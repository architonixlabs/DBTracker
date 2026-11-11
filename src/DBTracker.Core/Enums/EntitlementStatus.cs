namespace DBTracker.Core.Enums;

/// <summary>Whether an instance has active paid support on file. Independent of SupportPhase: a Mainstream product can have no contract, and an out-of-support one can have active Extended coverage.</summary>
public enum EntitlementStatus
{
    /// <summary>No contract linked at all. Not the same claim as Expired.</summary>
    NoContractOnFile,
    Active,
    ExpiringSoon,
    Expired
}
