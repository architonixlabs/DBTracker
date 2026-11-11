namespace DBTracker.Core.Enums;

/// <summary>Classification of a source-to-target pairing. No matching reviewed rule always means Unknown, never Supported.</summary>
public enum PathSupportStatus
{
    Unknown,
    Supported,
    Blocked
}
