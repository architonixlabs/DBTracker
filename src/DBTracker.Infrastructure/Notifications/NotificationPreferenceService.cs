using DBTracker.Core.Entities;
using DBTracker.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.Notifications;

/// <summary>Per-user quiet hours. A user can only read and change their own row.</summary>
public class NotificationPreferenceService(IDbContextFactory<DBTrackerDbContext> factory)
{
    /// <summary>The saved preference, or an unsaved default (quiet hours off) when the user has none.</summary>
    public async Task<NotificationPreference> GetAsync(string userId)
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.NotificationPreferences.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId)
            ?? new NotificationPreference { UserId = userId, TimeZoneId = TimeZoneInfo.Local.Id };
    }

    /// <summary>Returns an error message instead of saving when the times or zone are invalid.</summary>
    public async Task<string?> SaveAsync(string userId, bool enabled, int startMinute, int endMinute, string timeZoneId, bool criticalBypasses)
    {
        if (!QuietHoursEvaluator.IsValidMinute(startMinute) || !QuietHoursEvaluator.IsValidMinute(endMinute))
        {
            return "Enter start and end times as HH:mm, for example 22:00.";
        }
        if (enabled && startMinute == endMinute)
        {
            return "Start and end can't be the same time.";
        }
        if (!TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out _))
        {
            return "Choose a time zone from the list.";
        }

        using var db = await factory.CreateDbContextAsync();
        var pref = await db.NotificationPreferences.FirstOrDefaultAsync(p => p.UserId == userId);
        if (pref is null)
        {
            pref = new NotificationPreference { UserId = userId };
            db.NotificationPreferences.Add(pref);
        }

        pref.QuietHoursEnabled = enabled;
        pref.QuietStartMinute = startMinute;
        pref.QuietEndMinute = endMinute;
        pref.TimeZoneId = timeZoneId;
        pref.CriticalBypassesQuietHours = criticalBypasses;
        pref.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return null;
    }

    /// <summary>Users whose quiet hours hold back an email of this severity right now.</summary>
    public async Task<HashSet<string>> GetUsersHoldingEmailAsync(IEnumerable<string> userIds, bool isCritical, DateTime nowUtc)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        using var db = await factory.CreateDbContextAsync();
        var prefs = await db.NotificationPreferences.AsNoTracking()
            .Where(p => p.QuietHoursEnabled && ids.Contains(p.UserId))
            .ToListAsync();

        return prefs
            .Where(p => QuietHoursEvaluator.ShouldHoldEmail(
                QuietHoursEvaluator.IsQuiet(p.QuietHoursEnabled, p.QuietStartMinute, p.QuietEndMinute, QuietHoursEvaluator.ResolveZone(p.TimeZoneId), nowUtc),
                isCritical, p.CriticalBypassesQuietHours))
            .Select(p => p.UserId)
            .ToHashSet();
    }
}
