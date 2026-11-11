namespace DBTracker.Core.Services;

/// <summary>What a report run covers, so a filtered view is never sent when everything was meant, or the reverse. Preview, download and send all use it.</summary>
public record ReportScope(bool IsFullInventory, IReadOnlySet<int> InstanceIds, string Description)
{
    public static ReportScope Full(string description = "Full inventory") => new(true, new HashSet<int>(), description);

    public static ReportScope Filtered(IEnumerable<int> instanceIds, string description) =>
        new(false, instanceIds.ToHashSet(), description);

    public bool Includes(int instanceId) => IsFullInventory || InstanceIds.Contains(instanceId);
}
