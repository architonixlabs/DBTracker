namespace DBTracker.Core.Enums;

/// <summary>What slice of instances a notification subscription covers.</summary>
public enum SubscriptionScope
{
    AllInstances,
    Instance,
    Company,

    /// <summary>Instances owned by a specific Team (U21) — membership narrows what's surfaced, same as Company; it never grants visibility beyond the viewer's role.</summary>
    Team
}
