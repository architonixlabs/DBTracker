namespace DBTracker.Core.Enums;

/// <summary>Business criticality, separate from deployment tier. Unknown is the default and is never treated as Low.</summary>
public enum InstanceCriticality
{
    Unknown,
    Low,
    Medium,
    High,
    Critical
}
