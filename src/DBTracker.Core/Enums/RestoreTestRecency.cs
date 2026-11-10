namespace DBTracker.Core.Enums;

/// <summary>Restore-test coverage freshness (U12) — distinct from ordinary backup health.</summary>
public enum RestoreTestRecency
{
    NeverTested,
    Current,
    Overdue
}
