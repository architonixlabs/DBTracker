using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>
/// A one-time, short-lived token backing the "scan this QR code on your phone to sign in" flow.
/// The desktop (unauthenticated) creates it and polls its status; the phone (already authenticated
/// from a normal login) approves or denies it; only then does the desktop redeem it for a session —
/// this never bypasses password login, it just lets an already-trusted device vouch for a new one.
/// </summary>
public class DeviceLinkToken
{
    public int Id { get; set; }

    public string Token { get; set; } = string.Empty;
    public DeviceLinkStatus Status { get; set; } = DeviceLinkStatus.Pending;

    public string? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; } = DateTime.UtcNow.AddMinutes(5);
}
