namespace DBTracker.Core.Enums;

/// <summary>Key-rotation recency, separate from certificate expiry: a certificate far from expiry can still have a key overdue for rotation.</summary>
public enum KeyRotationStatus
{
    /// <summary>No rotation interval and/or no last-rotation date recorded.</summary>
    Unknown,
    Current,
    Overdue
}
