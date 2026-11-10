using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class ChangeWindowConflictDetectorTests
{
    private static readonly DateTime Start = new(2026, 12, 1, 2, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2026, 12, 1, 4, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void NoSiblingWindows_NoMaintenance_NoConflicts()
    {
        var result = ChangeWindowConflictDetector.Detect(Start, End, [], []);

        Assert.False(result.HasFreezeConflicts);
        Assert.Empty(result.MaintenanceOverlaps);
    }

    [Fact]
    public void OverlappingSiblingWindow_IsAFreezeConflict()
    {
        var sibling = new SiblingChangeWindow(1, "Other upgrade", Start.AddHours(1), End.AddHours(1));

        var result = ChangeWindowConflictDetector.Detect(Start, End, [sibling], []);

        Assert.True(result.HasFreezeConflicts);
        Assert.Contains("Other upgrade", result.FreezeConflicts[0]);
    }

    [Fact]
    public void AdjacentNonOverlappingWindow_IsNotAConflict()
    {
        // Sibling ends exactly when the candidate starts — back-to-back, not overlapping.
        var sibling = new SiblingChangeWindow(1, "Earlier window", Start.AddHours(-2), Start);

        var result = ChangeWindowConflictDetector.Detect(Start, End, [sibling], []);

        Assert.False(result.HasFreezeConflicts);
    }

    [Fact]
    public void WindowEntirelyContainedWithinAnother_StillConflicts()
    {
        var sibling = new SiblingChangeWindow(1, "Wide window", Start.AddHours(-5), End.AddHours(5));

        var result = ChangeWindowConflictDetector.Detect(Start, End, [sibling], []);

        Assert.True(result.HasFreezeConflicts);
    }

    [Fact]
    public void ActiveMaintenanceWindow_IsReportedSeparately_NotAsAFreezeConflict()
    {
        var maintenance = new InstanceMaintenanceWindow("PROD-SQL-01", Start.AddHours(1), "Patch Tuesday");

        var result = ChangeWindowConflictDetector.Detect(Start, End, [], [maintenance]);

        Assert.False(result.HasFreezeConflicts);
        Assert.Single(result.MaintenanceOverlaps);
        Assert.Contains("does not by itself make the change safe", result.MaintenanceOverlaps[0]);
    }

    [Fact]
    public void MaintenanceEndingBeforeCandidateStarts_IsNotAnOverlap()
    {
        var maintenance = new InstanceMaintenanceWindow("PROD-SQL-01", Start.AddHours(-1), "Already over");

        var result = ChangeWindowConflictDetector.Detect(Start, End, [], [maintenance]);

        Assert.Empty(result.MaintenanceOverlaps);
    }

    [Fact]
    public void NoMaintenanceConfigured_NullMaintenanceUntil_NeverOverlaps()
    {
        var maintenance = new InstanceMaintenanceWindow("PROD-SQL-01", null, "");

        var result = ChangeWindowConflictDetector.Detect(Start, End, [], [maintenance]);

        Assert.Empty(result.MaintenanceOverlaps);
    }
}
