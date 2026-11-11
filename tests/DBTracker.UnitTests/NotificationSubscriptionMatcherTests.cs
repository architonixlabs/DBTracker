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

    [Fact]
    public void TeamScope_MatchesOnlyThatTeamId()
    {
        var sub = new NotificationSubscription { Scope = SubscriptionScope.Team, TeamId = 3 };
        Assert.True(NotificationSubscriptionMatcher.Matches(sub, 1, "Acme", ownerTeamId: 3));
        Assert.False(NotificationSubscriptionMatcher.Matches(sub, 1, "Acme", ownerTeamId: 4));
    }

    [Fact]
    public void TeamScope_NullOwnerTeamId_NeverMatches()
    {
        var sub = new NotificationSubscription { Scope = SubscriptionScope.Team, TeamId = 3 };
        Assert.False(NotificationSubscriptionMatcher.Matches(sub, 1, "Acme", ownerTeamId: null));
    }

    [Theory]
    [InlineData(NotificationChannel.Email, true)]
    [InlineData(NotificationChannel.Both, true)]
    [InlineData(NotificationChannel.InApp, false)]
    public void WantsEmail_ChecksChannel(NotificationChannel channel, bool expected)
    {
        Assert.Equal(expected, NotificationSubscriptionMatcher.WantsEmail(new NotificationSubscription { Channel = channel }));
    }

    [Fact]
    public void Inbox_NoSubscriptions_ShowsEverything()
    {
        Assert.True(NotificationSubscriptionMatcher.ShowsInInbox(Array.Empty<NotificationSubscription>(), 1, "Acme"));
        Assert.False(NotificationSubscriptionMatcher.InboxIsOptedOut(Array.Empty<NotificationSubscription>()));
    }

    [Fact]
    public void Inbox_EmailOnlySubscription_DoesNotFillInbox()
    {
        var subs = new[] { new NotificationSubscription { Scope = SubscriptionScope.Instance, DatabaseInstanceId = 1, Channel = NotificationChannel.Email } };

        Assert.False(NotificationSubscriptionMatcher.ShowsInInbox(subs, 1, "Acme"));
        Assert.True(NotificationSubscriptionMatcher.InboxIsOptedOut(subs));
    }

    [Theory]
    [InlineData(NotificationChannel.InApp)]
    [InlineData(NotificationChannel.Both)]
    public void Inbox_InAppOrBoth_ShowsMatchingOnly(NotificationChannel channel)
    {
        var subs = new[] { new NotificationSubscription { Scope = SubscriptionScope.Team, TeamId = 5, Channel = channel } };

        Assert.True(NotificationSubscriptionMatcher.ShowsInInbox(subs, 1, "Acme", ownerTeamId: 5));
        Assert.False(NotificationSubscriptionMatcher.ShowsInInbox(subs, 1, "Acme", ownerTeamId: 6));
        Assert.False(NotificationSubscriptionMatcher.InboxIsOptedOut(subs));
    }

    [Fact]
    public void Inbox_MixedChannels_OnlyInAppSubscriptionsCount()
    {
        var subs = new[]
        {
            new NotificationSubscription { Scope = SubscriptionScope.Company, Company = "Acme", Channel = NotificationChannel.Email },
            new NotificationSubscription { Scope = SubscriptionScope.Instance, DatabaseInstanceId = 9, Channel = NotificationChannel.InApp },
        };

        Assert.False(NotificationSubscriptionMatcher.ShowsInInbox(subs, 1, "Acme"));
        Assert.True(NotificationSubscriptionMatcher.ShowsInInbox(subs, 9, "Other"));
    }
}
