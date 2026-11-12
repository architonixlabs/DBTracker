using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.Notifications;

public record NotificationInboxItem(int Id, int? DatabaseInstanceId, string InstanceName, string Message, NotificationSeverity Severity, DateTime CreatedAtUtc, bool IsRead);
/// <param name="InboxOptedOut">The user has subscriptions but none deliver in-app, so the inbox is empty by their choice.</param>
public record NotificationInboxPage(IReadOnlyList<NotificationInboxItem> Items, int TotalCount, int UnreadCount, bool InboxOptedOut = false);

/// <summary>Uses a short-lived DbContext per call because NotificationBell renders beside every page's own scoped context, and sharing one would race.</summary>
/// <remarks>Notifications are broadcast; read state is per user. On a user's first visit, the notifications that already exist are marked read once, so nobody starts with a backlog of old alerts; later ones arrive unread.</remarks>
public class NotificationService(IDbContextFactory<DBTrackerDbContext> factory)
{
    public async Task<NotificationInboxPage> GetInboxAsync(string userId, int page = 1, int pageSize = 20)
    {
        using var db = await factory.CreateDbContextAsync();
        await EnsureBackfilledAsync(db, userId);

        var subscriptions = await SubscriptionsAsync(db, userId);
        var visible = Visible(db, subscriptions);

        // Filtering, counting and paging run in the database; only the requested page is loaded.
        var totalCount = await visible.CountAsync();
        var unreadCount = await Unread(db, visible, userId).CountAsync();
        var pageItems = await visible
            .OrderByDescending(n => n.CreatedAtUtc).ThenByDescending(n => n.Id)
            .Skip(Math.Max(0, page - 1) * Math.Max(1, pageSize))
            .Take(Math.Max(1, pageSize))
            .Select(n => new NotificationInboxItem(n.Id, n.DatabaseInstanceId, n.InstanceName, n.Message, n.Severity, n.CreatedAtUtc,
                db.NotificationReadStates.Any(r => r.UserId == userId && r.NotificationId == n.Id)))
            .ToListAsync();

        return new NotificationInboxPage(pageItems, totalCount, unreadCount, NotificationSubscriptionMatcher.InboxIsOptedOut(subscriptions));
    }

    /// <summary>Bell-dropdown convenience: most recent page of this user's personal inbox.</summary>
    public async Task<List<NotificationInboxItem>> GetRecentAsync(string userId, int take = 20) =>
        (await GetInboxAsync(userId, page: 1, pageSize: take)).Items.ToList();

    public async Task<int> UnreadCountAsync(string userId)
    {
        using var db = await factory.CreateDbContextAsync();
        await EnsureBackfilledAsync(db, userId);
        var subscriptions = await SubscriptionsAsync(db, userId);
        return await Unread(db, Visible(db, subscriptions), userId).CountAsync();
    }

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

        var subscriptions = await SubscriptionsAsync(db, userId);
        // Only the ids of this user's unread, visible notifications come back.
        var unreadIds = await Unread(db, Visible(db, subscriptions), userId).Select(n => n.Id).ToListAsync();

        var now = DateTime.UtcNow;
        db.NotificationReadStates.AddRange(unreadIds.Select(id => new NotificationReadState { UserId = userId, NotificationId = id, ReadAtUtc = now }));
        await db.SaveChangesAsync();
    }

    private static Task<List<NotificationSubscription>> SubscriptionsAsync(DBTrackerDbContext db, string userId) =>
        db.NotificationSubscriptions.AsNoTracking().Where(s => s.UserId == userId).ToListAsync();

    private static IQueryable<Notification> Visible(DBTrackerDbContext db, IReadOnlyCollection<NotificationSubscription> subscriptions) =>
        db.Notifications.AsNoTracking().Where(NotificationInboxFilter.For(subscriptions));

    private static IQueryable<Notification> Unread(DBTrackerDbContext db, IQueryable<Notification> visible, string userId) =>
        visible.Where(n => !db.NotificationReadStates.Any(r => r.UserId == userId && r.NotificationId == n.Id));

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
