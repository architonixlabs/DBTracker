using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>Whether a subscription applies to an instance or company. Only narrows an already-broadcast notification; it can't surface anything the subscriber couldn't already see.</summary>
public static class NotificationSubscriptionMatcher
{
    public static bool Matches(NotificationSubscription subscription, int instanceId, string company, int? ownerTeamId = null) => subscription.Scope switch
    {
        SubscriptionScope.AllInstances => true,
        SubscriptionScope.Instance => subscription.DatabaseInstanceId == instanceId,
        SubscriptionScope.Company => !string.IsNullOrWhiteSpace(company) &&
            string.Equals(subscription.Company, company, StringComparison.OrdinalIgnoreCase),
        // Team subscriptions narrow too; they never widen what the subscriber's role can see.
        SubscriptionScope.Team => ownerTeamId is not null && subscription.TeamId == ownerTeamId,
        _ => false
    };

    public static bool AnyMatches(IEnumerable<NotificationSubscription> subscriptions, int instanceId, string company, int? ownerTeamId = null) =>
        subscriptions.Any(s => Matches(s, instanceId, company, ownerTeamId));

    public static bool WantsEmail(NotificationSubscription subscription) =>
        subscription.Channel is NotificationChannel.Email or NotificationChannel.Both;
}
