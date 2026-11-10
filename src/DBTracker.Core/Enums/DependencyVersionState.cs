namespace DBTracker.Core.Enums;

/// <summary>
/// Lets a mixed-version transition (D07) be recorded honestly: an instance mid-migration can have
/// both a Legacy and a Current/Transitioning dependency row for the same named thing at once,
/// rather than forcing one row to be silently overwritten.
/// </summary>
public enum DependencyVersionState
{
    Unknown,
    Current,
    Transitioning,
    Legacy
}
