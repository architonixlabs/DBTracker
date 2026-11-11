namespace DBTracker.Core.Enums;

/// <summary>How recent the last successful restore test is, separate from backup health.</summary>
public enum RestoreTestRecency
{
    NeverTested,
    Current,
    Overdue
}
