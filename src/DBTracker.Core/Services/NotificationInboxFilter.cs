using System.Linq.Expressions;
using DBTracker.Core.Entities;

namespace DBTracker.Core.Services;

/// <summary>The same rule as <see cref="NotificationSubscriptionMatcher.ShowsInInbox"/>, as an expression a database can run,
/// so the inbox filters, counts and pages in the query instead of loading every notification.</summary>
public static class NotificationInboxFilter
{
    public static Expression<Func<Notification, bool>> For(IReadOnlyCollection<NotificationSubscription> subscriptions)
    {
        // No subscriptions is the default: every alert.
        if (subscriptions.Count == 0)
        {
            return _ => true;
        }

        var inApp = subscriptions.Where(NotificationSubscriptionMatcher.WantsInApp).ToList();
        if (inApp.Count == 0)
        {
            return _ => false;
        }

        if (inApp.Any(s => s.Scope == Enums.SubscriptionScope.AllInstances))
        {
            return _ => true;
        }

        var instanceIds = inApp.Where(s => s.Scope == Enums.SubscriptionScope.Instance && s.DatabaseInstanceId is not null)
            .Select(s => s.DatabaseInstanceId!.Value).Distinct().ToList();
        // Company matching ignores case, whatever the database collation.
        var companies = inApp.Where(s => s.Scope == Enums.SubscriptionScope.Company && !string.IsNullOrWhiteSpace(s.Company))
            .Select(s => s.Company.ToLowerInvariant()).Distinct().ToList();
        var teamIds = inApp.Where(s => s.Scope == Enums.SubscriptionScope.Team && s.TeamId is not null)
            .Select(s => s.TeamId!.Value).Distinct().ToList();

        return n =>
            (n.DatabaseInstanceId != null && instanceIds.Contains(n.DatabaseInstanceId.Value)) ||
            companies.Contains(n.Company.ToLower()) ||
            (n.OwnerTeamId != null && teamIds.Contains(n.OwnerTeamId.Value));
    }
}
