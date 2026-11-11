namespace DBTracker.Core.Enums;

/// <summary>Lets a mid-migration instance hold both a Legacy and a Current/Transitioning row for the same dependency instead of overwriting one.</summary>
public enum DependencyVersionState
{
    Unknown,
    Current,
    Transitioning,
    Legacy
}
