namespace DBTracker.Core.Enums;

/// <summary>Recorded recovery-copy evidence compared against a retention policy.</summary>
public enum RetentionCoverageStatus
{
    /// <summary>No policy to compare against, or no copy evidence recorded at all.</summary>
    Unknown,

    /// <summary>At least one copy is currently Accessible, with evidence old enough and fresh enough to support the required retention window.</summary>
    Compliant,

    /// <summary>No currently accessible copy supports the retention window. A copy discovered once but now unverified doesn't count.</summary>
    Gap
}
