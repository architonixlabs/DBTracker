namespace DBTracker.Core.Enums;

/// <summary>Computed health of an instance's backup posture — not persisted, derived from BackupRecords.</summary>
public enum BackupHealth
{
    OnTrack,
    Overdue,
    Failed,
    NeverBackedUp
}
