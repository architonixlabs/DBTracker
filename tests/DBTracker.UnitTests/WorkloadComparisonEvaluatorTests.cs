using DBTracker.Core.Entities;
using DBTracker.Core.Enums;
using DBTracker.Core.Services;
using Xunit;

namespace DBTracker.UnitTests;

public class WorkloadComparisonEvaluatorTests
{
    private static readonly DateTime Now = new(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc);

    private static CapacityMeasurement M(int id, double value, int daysAgo, string unit = "ms", CapacityMeasurementType type = CapacityMeasurementType.PerformanceBaseline,
        string period = "p95 over 1h", int instanceId = 1) =>
        new() { Id = id, DatabaseInstanceId = instanceId, Value = value, MeasuredAtUtc = Now.AddDays(-daysAgo), Unit = unit, MeasurementType = type, MeasurementPeriodDescription = period };

    [Fact]
    public void SameTypeAndUnit_ReportsChangeAndPercent()
    {
        var result = WorkloadComparisonEvaluator.Compare(M(1, 40, 10), M(2, 50, 1), Now);

        Assert.True(result.IsComparable);
        Assert.Equal(10, result.Change);
        Assert.Equal(25, result.ChangePercent!.Value, 3);
        Assert.Empty(result.Warnings);
        Assert.Contains("+25.0%", result.Summary);
    }

    [Fact]
    public void DifferentUnits_AreNotComparable()
    {
        var result = WorkloadComparisonEvaluator.Compare(M(1, 40, 10, unit: "ms"), M(2, 0.05, 1, unit: "s"), Now);

        Assert.False(result.IsComparable);
        Assert.Contains(result.Warnings, w => w.Contains("Different units"));
    }

    [Fact]
    public void PostBeforePre_IsNotComparable()
    {
        var result = WorkloadComparisonEvaluator.Compare(M(1, 40, 1), M(2, 50, 10), Now);

        Assert.False(result.IsComparable);
    }

    [Fact]
    public void DifferentPeriods_AndStaleData_AreWarnings()
    {
        var result = WorkloadComparisonEvaluator.Compare(M(1, 40, 200, period: "daily avg"), M(2, 50, 120, period: "p95 over 1h"), Now);

        Assert.True(result.IsComparable);
        Assert.Contains(result.Warnings, w => w.Contains("Different measurement periods"));
        Assert.Contains(result.Warnings, w => w.Contains("more than"));
    }

    [Fact]
    public void MissingSide_SaysWhatIsMissing()
    {
        var result = WorkloadComparisonEvaluator.Compare(M(1, 40, 10), null, Now);

        Assert.False(result.IsComparable);
        Assert.Contains("post-change", result.Summary);
    }

    [Fact]
    public void ZeroBaseline_GivesNoPercent()
    {
        var result = WorkloadComparisonEvaluator.Compare(M(1, 0, 10), M(2, 5, 1), Now);

        Assert.Null(result.ChangePercent);
    }

    [Fact]
    public void ValidateForSave_RejectsOtherInstancesAndSameMeasurement()
    {
        Assert.NotEmpty(WorkloadComparisonEvaluator.ValidateForSave(null, null, 1));
        Assert.NotEmpty(WorkloadComparisonEvaluator.ValidateForSave(M(1, 1, 1, instanceId: 2), null, 1));
        Assert.NotEmpty(WorkloadComparisonEvaluator.ValidateForSave(M(1, 1, 2), M(1, 1, 2), 1));
        Assert.Empty(WorkloadComparisonEvaluator.ValidateForSave(M(1, 1, 2), null, 1));
    }
}
