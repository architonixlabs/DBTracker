using DBTracker.Core.Enums;

namespace DBTracker.Core.Services;

/// <summary>Another scheduled change window sharing at least one instance with the candidate.</summary>
public record SiblingChangeWindow(int Id, string Title, DateTime PlannedStartUtc, DateTime PlannedEndUtc);

/// <summary>One instance's existing maintenance window, if any.</summary>
public record InstanceMaintenanceWindow(string InstanceName, DateTime? MaintenanceUntilUtc, string MaintenanceReason);

/// <summary>An active change freeze. A null environment covers every environment.</summary>
public record ChangeFreezeWindow(int Id, string Name, DateTime StartUtc, DateTime EndUtc, EnvironmentTier? AppliesToEnvironment, string Reason);

public record ChangeWindowConflictResult(
    IReadOnlyList<string> WindowOverlaps,
    IReadOnlyList<string> FreezeConflicts,
    IReadOnlyList<string> MaintenanceOverlaps)
{
    public bool HasWindowOverlaps => WindowOverlaps.Count > 0;
    public bool HasFreezeConflicts => FreezeConflicts.Count > 0;

    /// <summary>A freeze blocks approving or starting the window unless an active emergency exception is linked. Overlaps with other
    /// windows and maintenance windows are warnings for the planner, not blocks.</summary>
    public bool BlocksApproval(bool hasActiveEmergencyException) => HasFreezeConflicts && !hasActiveEmergencyException;
}

/// <summary>Overlap detection on [start, end) ranges against other change windows on shared instances, change freezes for the affected
/// environments, and maintenance windows. A maintenance overlap is reported separately, because maintenance only suppresses alerts and never
/// means a change is safe.</summary>
public static class ChangeWindowConflictDetector
{
    public static ChangeWindowConflictResult Detect(
        DateTime candidateStartUtc,
        DateTime candidateEndUtc,
        IReadOnlyList<SiblingChangeWindow> otherWindowsForSharedInstances,
        IReadOnlyList<InstanceMaintenanceWindow> instanceMaintenanceWindows,
        IReadOnlyList<ChangeFreezeWindow>? freezes = null,
        IReadOnlyCollection<EnvironmentTier>? affectedEnvironments = null)
    {
        var windowOverlaps = otherWindowsForSharedInstances
            .Where(w => Overlaps(candidateStartUtc, candidateEndUtc, w.PlannedStartUtc, w.PlannedEndUtc))
            .Select(w => $"Overlaps scheduled window \"{w.Title}\" ({w.PlannedStartUtc:yyyy-MM-dd HH:mm} to {w.PlannedEndUtc:yyyy-MM-dd HH:mm} UTC).")
            .ToList();

        var environments = affectedEnvironments ?? [];
        var freezeConflicts = (freezes ?? [])
            .Where(f => Overlaps(candidateStartUtc, candidateEndUtc, f.StartUtc, f.EndUtc))
            .Where(f => f.AppliesToEnvironment is null || environments.Contains(f.AppliesToEnvironment.Value))
            .Select(f => $"Falls in change freeze \"{f.Name}\" ({f.StartUtc:yyyy-MM-dd HH:mm} to {f.EndUtc:yyyy-MM-dd HH:mm} UTC, " +
                         $"{(f.AppliesToEnvironment is { } env ? env.ToString() : "all environments")})" +
                         (string.IsNullOrWhiteSpace(f.Reason) ? "." : $": {f.Reason}."))
            .ToList();

        var maintenanceOverlaps = instanceMaintenanceWindows
            .Where(m => m.MaintenanceUntilUtc is { } until && candidateStartUtc < until)
            .Select(m => $"{m.InstanceName} has an active/planned maintenance window until {m.MaintenanceUntilUtc:yyyy-MM-dd HH:mm} UTC ({(string.IsNullOrWhiteSpace(m.MaintenanceReason) ? "no reason recorded" : m.MaintenanceReason)}). That window does not by itself make the change safe.")
            .ToList();

        return new ChangeWindowConflictResult(windowOverlaps, freezeConflicts, maintenanceOverlaps);
    }

    private static bool Overlaps(DateTime aStart, DateTime aEnd, DateTime bStart, DateTime bEnd) =>
        aStart < bEnd && bStart < aEnd;
}
