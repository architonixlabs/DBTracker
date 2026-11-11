using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.Notifications;

public record NotificationInboxItem(int Id, int? DatabaseInstanceId, string InstanceName, string Message, NotificationSeverity Severity, DateTime CreatedAtUtc, bool IsRead);
public record NotificationInboxPage(IReadOnlyList<NotificationInboxItem> Items, int TotalCount, int UnreadCount);

/// <summary>Uses a short-lived DbContext per call because NotificationBell renders beside every page's own scoped context, and sharing one would race.</summary>
/// <remarks>Notifications are broadcast; read state is per user. On a user's first visit, the notifications that already exist are marked read once, so nobody starts with a backlog of old alerts; later ones arrive unread.</remarks>
public class NotificationService(IDbContextFactory<DBTrackerDbContext> factory)
{
    public async Task<NotificationInboxPage> GetInboxAsync(string userId, int page = 1, int pageSize = 20)
    {
        using var db = await factory.CreateDbContextAsync();
        await EnsureBackfilledAsync(db, userId);

        var subscriptions = await db.NotificationSubscriptions.AsNoTracking().Where(s => s.UserId == userId).ToListAsync();
        var readIds = await db.NotificationReadStates.AsNoTracking().Where(s => s.UserId == userId).Select(s => s.NotificationId).ToListAsync();
        var readSet = readIds.ToHashSet();

        var all = await db.Notifications.AsNoTracking().OrderByDescending(n => n.CreatedAtUtc).ToListAsync();
        var visible = subscriptions.Count == 0
            ? all
            : all.Where(n => NotificationSubscriptionMatcher.AnyMatches(subscriptions, n.DatabaseInstanceId ?? 0, n.Company, n.OwnerTeamId)).ToList();

        var unreadCount = visible.Count(n => !readSet.Contains(n.Id));
        var pageItems = visible
            .Skip(Math.Max(0, page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NotificationInboxItem(n.Id, n.DatabaseInstanceId, n.InstanceName, n.Message, n.Severity, n.CreatedAtUtc, readSet.Contains(n.Id)))
            .ToList();

        return new NotificationInboxPage(pageItems, visible.Count, unreadCount);
    }

    /// <summary>Bell-dropdown convenience: most recent page of this user's personal inbox.</summary>
    public async Task<List<NotificationInboxItem>> GetRecentAsync(string userId, int take = 20) =>
        (await GetInboxAsync(userId, page: 1, pageSize: take)).Items.ToList();

    public async Task<int> UnreadCountAsync(string userId) =>
        (await GetInboxAsync(userId, page: 1, pageSize: 1)).UnreadCount;

    public async Task MarkReadAsync(string userId, int notificationId)
    {
        using var db = await factory.CreateDbContextAsync();
        var already = await db.NotificationReadStates.AnyAsync(s => s.UserId == userId && s.NotificationId == notificationId);
        if (!already)
        {
            db.NotificationReadStates.Add(new NotificationReadState { UserId = userId, NotificationId = notificationId });
            await db.SaveChangesAsync();
        }
    }

    public async Task MarkAllReadAsync(string userId)
    {
        using var db = await factory.CreateDbContextAsync();
        await EnsureBackfilledAsync(db, userId);

        var subscriptions = await db.NotificationSubscriptions.AsNoTracking().Where(s => s.UserId == userId).ToListAsync();
        var readIds = await db.NotificationReadStates.AsNoTracking().Where(s => s.UserId == userId).Select(s => s.NotificationId).ToHashSetAsync();

        var all = await db.Notifications.AsNoTracking().Select(n => new { n.Id, n.DatabaseInstanceId, n.Company, n.OwnerTeamId }).ToListAsync();
        var visible = subscriptions.Count == 0
            ? all
            : all.Where(n => NotificationSubscriptionMatcher.AnyMatches(subscriptions, n.DatabaseInstanceId ?? 0, n.Company, n.OwnerTeamId)).ToList();

        var now = DateTime.UtcNow;
        var newlyRead = visible.Where(n => !readIds.Contains(n.Id))
            .Select(n => new NotificationReadState { UserId = userId, NotificationId = n.Id, ReadAtUtc = now });
        db.NotificationReadStates.AddRange(newlyRead);
        await db.SaveChangesAsync();
    }

    public async Task<Notification> CreateAsync(DatabaseInstance instance, string message, NotificationSeverity severity)
    {
        using var db = await factory.CreateDbContextAsync();
        var notification = new Notification
        {
            DatabaseInstanceId = instance.Id,
            InstanceName = instance.Name,
            Company = instance.Company,
            OwnerTeamId = instance.OwnerTeamId,
            Message = message,
            Severity = severity
        };
        db.Notifications.Add(notification);
        await db.SaveChangesAsync();
        return notification;
    }

    /// <summary>Runs once per user, recorded in NotificationInboxStarts even when there is nothing to mark, so alerts
    /// raised after the first visit are never pre-marked read.</summary>
    private static async Task EnsureBackfilledAsync(DBTrackerDbContext db, string userId)
    {
        if (await db.NotificationInboxStarts.AnyAsync(s => s.UserId == userId))
        {
            return;
        }

        var now = DateTime.UtcNow;
        db.NotificationInboxStarts.Add(new NotificationInboxStart { UserId = userId, StartedAtUtc = now });

        // A user with read state from before this marker existed has already been backfilled; only record the marker.
        var hasAnyReadState = await db.NotificationReadStates.AnyAsync(s => s.UserId == userId);
        if (!hasAnyReadState)
        {
            var existingIds = await db.Notifications.Where(n => n.CreatedAtUtc <= now).Select(n => n.Id).ToListAsync();
            db.NotificationReadStates.AddRange(existingIds.Select(id => new NotificationReadState { NotificationId = id, UserId = userId, ReadAtUtc = now }));
        }

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // The bell and the inbox page can start the same user's first visit at once; the other call already recorded it.
            db.ChangeTracker.Clear();
        }
    }
}
