namespace DBTracker.Core.Services;

/// <summary>
/// What a report run actually covers (U09) — made explicit so "current filtered view" is never
/// silently sent when the viewer meant "everything", or vice versa. Preview, download and send all
/// derive from the same scope.
/// </summary>
public record ReportScope(bool IsFullInventory, IReadOnlySet<int> InstanceIds, string Description)
{
    public static ReportScope Full(string description = "Full inventory") => new(true, new HashSet<int>(), description);

    public static ReportScope Filtered(IEnumerable<int> instanceIds, string description) =>
        new(false, instanceIds.ToHashSet(), description);

    public bool Includes(int instanceId) => IsFullInventory || InstanceIds.Contains(instanceId);
}
