namespace DBTracker.Core.Enums;

/// <summary>
/// D15 — business criticality as its own explainable factor, distinct from EnvironmentTier (which
/// describes deployment tier, not business impact — a Staging instance can still be business-critical
/// for a specific test window, and not every Production instance carries equal weight). Unknown is the
/// default and a correct first-class state, never silently treated as Low.
/// </summary>
public enum InstanceCriticality
{
    Unknown,
    Low,
    Medium,
    High,
    Critical
}
