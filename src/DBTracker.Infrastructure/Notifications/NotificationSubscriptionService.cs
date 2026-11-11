using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Microsoft.EntityFrameworkCore;

namespace DBTracker.Infrastructure.Notifications;

/// <summary>Per-user subscriptions: which instances or companies a user follows, and whether they want email.</summary>
public class NotificationSubscriptionService(IDbContextFactory<DBTrackerDbContext> factory)
{
    public async Task<List<NotificationSubscription>> ListAsync(string userId)
    {
        using var db = await factory.CreateDbContextAsync();
        return await db.NotificationSubscriptions
            .AsNoTracking()
            .Include(s => s.DatabaseInstance)
            .Where(s => s.UserId == userId)
            .OrderBy(s => s.Scope).ThenBy(s => s.Company)
            .ToListAsync();
    }

    public async Task AddAsync(string userId, SubscriptionScope scope, int? instanceId, string company, NotificationChannel channel)
    {
        using var db = await factory.CreateDbContextAsync();
        db.NotificationSubscriptions.Add(new NotificationSubscription
        {
            UserId = userId,
            Scope = scope,
            DatabaseInstanceId = scope == SubscriptionScope.Instance ? instanceId : null,
            Company = scope == SubscriptionScope.Company ? company : string.Empty,
            Channel = channel
        });
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id, string userId)
    {
        using var db = await factory.CreateDbContextAsync();
        await db.NotificationSubscriptions.Where(s => s.Id == id && s.UserId == userId).ExecuteDeleteAsync();
    }

    /// <summary>Users with a matching Email/Both subscription, emailed alongside the instance contact. Only narrows who hears about a broadcast alert; never grants visibility.</summary>
    public async Task<List<string>> GetEmailSubscriberUserIdsAsync(int instanceId, string company, int? ownerTeamId = null)
    {
        using var db = await factory.CreateDbContextAsync();
        var subscriptions = await db.NotificationSubscriptions.AsNoTracking().ToListAsync();
        return subscriptions
            .Where(s => NotificationSubscriptionMatcher.WantsEmail(s) && NotificationSubscriptionMatcher.Matches(s, instanceId, company, ownerTeamId))
            .Select(s => s.UserId)
            .Distinct()
            .ToList();
    }
}
