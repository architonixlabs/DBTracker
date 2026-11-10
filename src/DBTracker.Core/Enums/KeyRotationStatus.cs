namespace DBTracker.Core.Enums;

/// <summary>
/// Key-rotation recency (D20) — a deliberately separate concept/enum from CertificateExpiryStatus.
/// A certificate can be far from expiring while its underlying key is overdue for rotation, and vice
/// versa; nothing here conflates the two.
/// </summary>
public enum KeyRotationStatus
{
    /// <summary>No rotation interval and/or no last-rotation date recorded.</summary>
    Unknown,
    Current,
    Overdue
}
