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

    public static bool WantsInApp(NotificationSubscription subscription) =>
        subscription.Channel is NotificationChannel.InApp or NotificationChannel.Both;

    /// <summary>Whether a notification belongs in this user's in-app inbox. No subscriptions at all is the default: every alert.
    /// Otherwise only in-app (InApp or Both) subscriptions count, so an email-only subscription never fills the inbox.</summary>
    public static bool ShowsInInbox(IReadOnlyCollection<NotificationSubscription> subscriptions, int instanceId, string company, int? ownerTeamId = null) =>
        subscriptions.Count == 0 ||
        subscriptions.Any(s => WantsInApp(s) && Matches(s, instanceId, company, ownerTeamId));

    /// <summary>True when the user has subscriptions but none deliver in-app, so their inbox stays empty by choice.</summary>
    public static bool InboxIsOptedOut(IReadOnlyCollection<NotificationSubscription> subscriptions) =>
        subscriptions.Count > 0 && !subscriptions.Any(WantsInApp);
}
