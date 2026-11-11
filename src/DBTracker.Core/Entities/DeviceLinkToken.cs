using DBTracker.Core.Enums;

namespace DBTracker.Core.Entities;

/// <summary>A one-time, short-lived token for QR sign-in. An already signed-in phone approves it before the desktop can redeem it; password login is never bypassed.</summary>
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
