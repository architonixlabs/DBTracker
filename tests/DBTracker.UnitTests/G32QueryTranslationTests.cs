using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using DBTracker.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DBTracker.UnitTests;

/// <summary>G32: the inbox and restore-evidence rules must translate to SQL, not fall back to loading rows. ToQueryString needs no connection.</summary>
public class G32QueryTranslationTests
{
    private static DBTrackerDbContext NewContext() =>
        new(new DbContextOptionsBuilder<DBTrackerDbContext>().UseSqlServer("Server=unused;Database=unused;Trusted_Connection=True").Options);

    [Fact]
    public void InboxFilter_CountAndPage_TranslateToSql()
    {
        using var db = NewContext();
        List<NotificationSubscription> subscriptions =
        [
            new() { Scope = SubscriptionScope.Instance, DatabaseInstanceId = 7, Channel = NotificationChannel.InApp },
            new() { Scope = SubscriptionScope.Company, Company = "Contoso", Channel = NotificationChannel.Both },
            new() { Scope = SubscriptionScope.Team, TeamId = 3, Channel = NotificationChannel.InApp }
        ];

        var sql = db.Notifications.Where(NotificationInboxFilter.For(subscriptions))
            .Where(n => !db.NotificationReadStates.Any(r => r.UserId == "u1" && r.NotificationId == n.Id))
            .OrderByDescending(n => n.CreatedAtUtc).Skip(20).Take(20)
            .ToQueryString();

        Assert.Contains("LOWER", sql);
        Assert.Contains("NOT EXISTS", sql);
        Assert.Contains("OFFSET", sql);
    }

    [Fact]
    public void RestoreVerified_TranslatesToSql_WithTheCorrectionSubquery()
    {
        using var db = NewContext();

        var sql = db.RestoreEvents.Where(RestoreEvidence.IsVerified).Select(r => r.TimestampUtc).ToQueryString();

        Assert.Contains("RestoreEventCorrections", sql);
        Assert.Contains("COALESCE", sql);
    }
}
