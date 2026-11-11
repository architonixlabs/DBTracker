namespace DBTracker.Core.Enums;

/// <summary>What slice of instances a notification subscription covers.</summary>
public enum SubscriptionScope
{
    AllInstances,
    Instance,
    Company,

    /// <summary>Instances owned by a specific team. Narrows what's surfaced; never grants visibility beyond the viewer's role.</summary>
    Team
}
