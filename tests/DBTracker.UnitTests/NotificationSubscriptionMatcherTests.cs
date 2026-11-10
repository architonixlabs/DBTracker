using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class NotificationSubscriptionMatcherTests
{
    [Fact]
    public void AllInstances_MatchesAnyInstanceOrCompany()
    {
        var sub = new NotificationSubscription { Scope = SubscriptionScope.AllInstances };
        Assert.True(NotificationSubscriptionMatcher.Matches(sub, 42, "AnyCo"));
    }

    [Fact]
    public void InstanceScope_OnlyMatchesThatInstanceId()
    {
        var sub = new NotificationSubscription { Scope = SubscriptionScope.Instance, DatabaseInstanceId = 7 };
        Assert.True(NotificationSubscriptionMatcher.Matches(sub, 7, "Acme"));
        Assert.False(NotificationSubscriptionMatcher.Matches(sub, 8, "Acme"));
    }

    [Fact]
    public void CompanyScope_MatchesCaseInsensitively()
    {
        var sub = new NotificationSubscription { Scope = SubscriptionScope.Company, Company = "Acme Corp" };
        Assert.True(NotificationSubscriptionMatcher.Matches(sub, 1, "ACME CORP"));
        Assert.False(NotificationSubscriptionMatcher.Matches(sub, 1, "Other Co"));
    }

    [Fact]
    public void CompanyScope_EmptyNotificationCompany_NeverMatches()
    {
        var sub = new NotificationSubscription { Scope = SubscriptionScope.Company, Company = "Acme" };
        Assert.False(NotificationSubscriptionMatcher.Matches(sub, 1, ""));
    }

    [Fact]
    public void AnyMatches_TrueIfAnySubscriptionMatches()
    {
        var subs = new[]
        {
            new NotificationSubscription { Scope = SubscriptionScope.Instance, DatabaseInstanceId = 1 },
            new NotificationSubscription { Scope = SubscriptionScope.Company, Company = "Acme" },
        };
        Assert.True(NotificationSubscriptionMatcher.AnyMatches(subs, 999, "Acme"));
        Assert.False(NotificationSubscriptionMatcher.AnyMatches(subs, 999, "Other"));
    }

    [Theory]
    [InlineData(NotificationChannel.Email, true)]
    [InlineData(NotificationChannel.Both, true)]
    [InlineData(NotificationChannel.InApp, false)]
    public void WantsEmail_ChecksChannel(NotificationChannel channel, bool expected)
    {
        Assert.Equal(expected, NotificationSubscriptionMatcher.WantsEmail(new NotificationSubscription { Channel = channel }));
    }
}
