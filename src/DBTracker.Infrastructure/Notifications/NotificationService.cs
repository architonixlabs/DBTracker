using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.Notifications;

public record NotificationInboxItem(int Id, int? DatabaseInstanceId, string InstanceName, string Message, NotificationSeverity Severity, DateTime CreatedAtUtc, bool IsRead);
public record NotificationInboxPage(IReadOnlyList<NotificationInboxItem> Items, int TotalCount, int UnreadCount);

/// <summary>
/// Uses its own short-lived DbContext per call (via the factory) rather than an injected scoped
/// one — this service is used from NotificationBell, which renders on every page alongside that
/// page's own ambient scoped context, so sharing one would race.
///
/// Notifications themselves stay a shared/broadcast table (U07 didn't change who an alert is
/// about), but read state is now personal: NotificationReadState is a per-(notification,user) row,
/// and a user who subscribes (NotificationSubscription) only sees the slice they opted into. A
/// user's first-ever visit treats every notification that already existed at that moment as
/// already read — the "migration" of the old shared world, applied lazily per user rather than as
/// a single big-bang backfill — so nobody is confronted with years of backlog as unread; only
/// notifications created after that first visit are ever unread for them.
/// </summary>
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
            : all.Where(n => NotificationSubscriptionMatcher.AnyMatches(subscriptions, n.DatabaseInstanceId ?? 0, n.Company)).ToList();

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

        var all = await db.Notifications.AsNoTracking().Select(n => new { n.Id, n.DatabaseInstanceId, n.Company }).ToListAsync();
        var visible = subscriptions.Count == 0
            ? all
            : all.Where(n => NotificationSubscriptionMatcher.AnyMatches(subscriptions, n.DatabaseInstanceId ?? 0, n.Company)).ToList();

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
            Message = message,
            Severity = severity
        };
        db.Notifications.Add(notification);
        await db.SaveChangesAsync();
        return notification;
    }

    private static async Task EnsureBackfilledAsync(DBTrackerDbContext db, string userId)
    {
        var hasAnyReadState = await db.NotificationReadStates.AnyAsync(s => s.UserId == userId);
        if (hasAnyReadState)
        {
            return;
        }

        var existingIds = await db.Notifications.Select(n => n.Id).ToListAsync();
        if (existingIds.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        db.NotificationReadStates.AddRange(existingIds.Select(id => new NotificationReadState { NotificationId = id, UserId = userId, ReadAtUtc = now }));
        await db.SaveChangesAsync();
    }
}
