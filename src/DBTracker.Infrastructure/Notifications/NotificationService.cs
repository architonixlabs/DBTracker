using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.Notifications;

/// <summary>
/// Uses its own short-lived DbContext per call (via the factory) rather than an injected scoped
/// one — this service is used from NotificationBell, which renders on every page alongside that
/// page's own ambient scoped context, so sharing one would race.
/// </summary>
public class NotificationService(IDbContextFactory<DBTrackerDbContext> factory)
{
    public async Task<List<Notification>> GetRecentAsync(int take = 20)
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.Notifications
            .AsNoTracking()
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(take)
            .ToListAsync();
    }

    public async Task<int> UnreadCountAsync()
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.Notifications.CountAsync(n => !n.IsRead);
    }

    public async Task MarkAllReadAsync()
    {
        using var db = await factory.CreateDbContextAsync();
        await db.Notifications
            .Where(n => !n.IsRead)
            .ExecuteUpdateAsync(set => set.SetProperty(n => n.IsRead, true));
    }

    public async Task CreateAsync(DatabaseInstance instance, string message, NotificationSeverity severity)
    {
        using var db = await factory.CreateDbContextAsync();
        db.Notifications.Add(new Notification
        {
            DatabaseInstanceId = instance.Id,
            InstanceName = instance.Name,
            Message = message,
            Severity = severity
        });
        await db.SaveChangesAsync();
    }
}
