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
            .Include(s => s.Team)
            .Where(s => s.UserId == userId)
            .OrderBy(s => s.Scope).ThenBy(s => s.Company)
            .ToListAsync();
    }

    /// <summary>Returns an error message instead of saving when the scope's target is missing or doesn't exist.</summary>
    public async Task<string?> AddAsync(string userId, SubscriptionScope scope, int? instanceId, string company, NotificationChannel channel, int? teamId = null)
    {
        using var db = await factory.CreateDbContextAsync();
        if (scope == SubscriptionScope.Team && (teamId is null || !await db.Teams.AnyAsync(t => t.Id == teamId)))
        {
            return "Choose a team to follow.";
        }
        if (scope == SubscriptionScope.Instance && (instanceId is null || !await db.DatabaseInstances.AnyAsync(i => i.Id == instanceId)))
        {
            return "Choose an instance to follow.";
        }
        if (scope == SubscriptionScope.Company && string.IsNullOrWhiteSpace(company))
        {
            return "Choose a company to follow.";
        }

        db.NotificationSubscriptions.Add(new NotificationSubscription
        {
            UserId = userId,
            Scope = scope,
            DatabaseInstanceId = scope == SubscriptionScope.Instance ? instanceId : null,
            Company = scope == SubscriptionScope.Company ? company.Trim() : string.Empty,
            TeamId = scope == SubscriptionScope.Team ? teamId : null,
            Channel = channel
        });
        await db.SaveChangesAsync();
        return null;
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
