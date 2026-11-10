using DBTracker.Core.Entities;
using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>
/// Whether a subscription (U07) applies to a given instance/company. Purely a narrowing filter on
/// top of an already-broadcast notification — a subscription can never surface an instance the
/// subscriber wasn't already authorized to see, since the app has no per-instance row-level
/// security today (that's deferred to U21); it only decides what a user is told about.
/// </summary>
public static class NotificationSubscriptionMatcher
{
    public static bool Matches(NotificationSubscription subscription, int instanceId, string company) => subscription.Scope switch
    {
        SubscriptionScope.AllInstances => true,
        SubscriptionScope.Instance => subscription.DatabaseInstanceId == instanceId,
        SubscriptionScope.Company => !string.IsNullOrWhiteSpace(company) &&
            string.Equals(subscription.Company, company, StringComparison.OrdinalIgnoreCase),
        _ => false
    };

    public static bool AnyMatches(IEnumerable<NotificationSubscription> subscriptions, int instanceId, string company) =>
        subscriptions.Any(s => Matches(s, instanceId, company));

    public static bool WantsEmail(NotificationSubscription subscription) =>
        subscription.Channel is NotificationChannel.Email or NotificationChannel.Both;
}
