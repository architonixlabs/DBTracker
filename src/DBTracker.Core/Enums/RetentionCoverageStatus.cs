namespace DBTracker.Core.Enums;

/// <summary>Result of comparing recorded recovery-copy evidence against a retention policy (D17) — reuses U21's policy pattern rather than inventing a new one.</summary>
public enum RetentionCoverageStatus
{
    /// <summary>No policy to compare against, or no copy evidence recorded at all.</summary>
    Unknown,

    /// <summary>At least one copy is currently Accessible, with evidence old enough and fresh enough to support the required retention window.</summary>
    Compliant,

    /// <summary>No currently-accessible copy supports the required retention window — historical discovery of a now-unverified copy is explicitly not sufficient.</summary>
    Gap
}
