using System.Security.Cryptography;
using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.Identity;

/// <summary>QR phone sign-in. Uses a DbContext per call because it's hit from anonymous minimal-API endpoints, not a circuit.</summary>
public class DeviceLinkService(IDbContextFactory<DBTrackerDbContext> factory)
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(5);

    public async Task<string> CreateTokenAsync()
    {
        using var db = await factory.CreateDbContextAsync();

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        db.DeviceLinkTokens.Add(new DeviceLinkToken
        {
            Token = token,
            Status = DeviceLinkStatus.Pending,
            ExpiresAtUtc = DateTime.UtcNow.Add(TokenLifetime)
        });
        await db.SaveChangesAsync();
        return token;
    }

    public async Task<DeviceLinkStatus?> GetStatusAsync(string token)
    {
        using var db = await factory.CreateDbContextAsync();
        var entry = await db.DeviceLinkTokens.AsNoTracking().FirstOrDefaultAsync(t => t.Token == token);
        if (entry is null)
        {
            return null;
        }
        if (entry.Status == DeviceLinkStatus.Pending && entry.ExpiresAtUtc < DateTime.UtcNow)
        {
            return DeviceLinkStatus.Expired;
        }
        return entry.Status;
    }

    /// <summary>Called from the phone, which is already authenticated — marks the token approved for that user.</summary>
    public async Task<bool> ApproveAsync(string token, string userId)
    {
        using var db = await factory.CreateDbContextAsync();
        var entry = await db.DeviceLinkTokens.FirstOrDefaultAsync(t => t.Token == token);
        if (entry is null || entry.Status != DeviceLinkStatus.Pending || entry.ExpiresAtUtc < DateTime.UtcNow)
        {
            return false;
        }

        entry.Status = DeviceLinkStatus.Approved;
        entry.ApprovedByUserId = userId;
        entry.ApprovedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DenyAsync(string token)
    {
        using var db = await factory.CreateDbContextAsync();
        var entry = await db.DeviceLinkTokens.FirstOrDefaultAsync(t => t.Token == token);
        if (entry is null || entry.Status != DeviceLinkStatus.Pending)
        {
            return false;
        }

        entry.Status = DeviceLinkStatus.Denied;
        await db.SaveChangesAsync();
        return true;
    }

    /// <summary>Called by the desktop once polling shows Approved. Marks the token Redeemed immediately so it is single-use, then returns the user id.</summary>
    public async Task<string?> RedeemAsync(string token)
    {
        using var db = await factory.CreateDbContextAsync();
        var entry = await db.DeviceLinkTokens.FirstOrDefaultAsync(t => t.Token == token);
        if (entry is null || entry.Status != DeviceLinkStatus.Approved || entry.ApprovedByUserId is null)
        {
            return null;
        }

        entry.Status = DeviceLinkStatus.Redeemed;
        await db.SaveChangesAsync();
        return entry.ApprovedByUserId;
    }
}
