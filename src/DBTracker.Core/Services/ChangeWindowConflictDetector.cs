namespace DBTracker.Core.Services;

/// <summary>One other scheduled D12 ChangeWindow sharing at least one affected instance with the candidate window being checked.</summary>
public record SiblingChangeWindow(int Id, string Title, DateTime PlannedStartUtc, DateTime PlannedEndUtc);

/// <summary>One instance's existing (Epic 12) maintenance window, if any.</summary>
public record InstanceMaintenanceWindow(string InstanceName, DateTime? MaintenanceUntilUtc, string MaintenanceReason);

public record ChangeWindowConflictResult(
    IReadOnlyList<string> FreezeConflicts,
    IReadOnlyList<string> MaintenanceOverlaps)
{
    public bool HasFreezeConflicts => FreezeConflicts.Count > 0;
}

/// <summary>
/// Pure (no I/O) overlap detection for D12. Two windows conflict when their [start, end) ranges
/// overlap, regardless of which started first. A maintenance-window overlap is reported separately
/// and never folds into "conflict" — Epic 12's maintenance suppresses alerts, it does not prove a
/// change is safe, so it must stay visible rather than being treated as clearance.
/// </summary>
public static class ChangeWindowConflictDetector
{
    public static ChangeWindowConflictResult Detect(
        DateTime candidateStartUtc,
        DateTime candidateEndUtc,
        IReadOnlyList<SiblingChangeWindow> otherWindowsForSharedInstances,
        IReadOnlyList<InstanceMaintenanceWindow> instanceMaintenanceWindows)
    {
        var freezeConflicts = otherWindowsForSharedInstances
            .Where(w => Overlaps(candidateStartUtc, candidateEndUtc, w.PlannedStartUtc, w.PlannedEndUtc))
            .Select(w => $"Overlaps scheduled window \"{w.Title}\" ({w.PlannedStartUtc:yyyy-MM-dd HH:mm}–{w.PlannedEndUtc:HH:mm} UTC).")
            .ToList();

        var maintenanceOverlaps = instanceMaintenanceWindows
            .Where(m => m.MaintenanceUntilUtc is { } until && candidateStartUtc < until)
            .Select(m => $"{m.InstanceName} has an active/planned maintenance window until {m.MaintenanceUntilUtc:yyyy-MM-dd HH:mm} UTC ({(string.IsNullOrWhiteSpace(m.MaintenanceReason) ? "no reason recorded" : m.MaintenanceReason)}) — this does not by itself make the change safe.")
            .ToList();

        return new ChangeWindowConflictResult(freezeConflicts, maintenanceOverlaps);
    }

    private static bool Overlaps(DateTime aStart, DateTime aEnd, DateTime bStart, DateTime bEnd) =>
        aStart < bEnd && bStart < aEnd;
}
